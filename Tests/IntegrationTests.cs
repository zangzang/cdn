using FluentAssertions;
using HttpClientHelper.Core;
using Xunit;
using Flurl.Http.Testing;
using System.Text;

namespace Tests
{
    /// <summary>
    /// 통합 테스트: API가 다른 API를 호출하는 시나리오
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly HttpTest _httpTest;

        public IntegrationTests()
        {
            _httpTest = new HttpTest();
        }

        public void Dispose()
        {
            _httpTest?.Dispose();
        }

        [Fact]
        public async Task ApiChain_FirstApiCallsSecondApi_ShouldSucceed()
        {
            // Arrange
            // 첫 번째 API가 두 번째 API를 호출하는 시나리오
            var secondApiResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "Second API Success",
                Data = "processed-data",
                StatusCode = 200
            };

            var firstApiResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "First API Success",
                Data = "final-result",
                StatusCode = 200
            };

            // 두 번째 API 응답 설정
            _httpTest.RespondWithJson(secondApiResponse, 200);
            
            // 첫 번째 API 응답 설정
            _httpTest.RespondWithJson(firstApiResponse, 200);

            // Act
            var firstApiHelper = new FlurlHttpHelper("https://api.first.com");
            var secondApiHelper = new FlurlHttpHelper("https://api.second.com");

            // 두 번째 API 호출
            var secondResult = await secondApiHelper.PostAsync<ApiResponse<string>>(
                "/process",
                new { data = "input-data" }
            );

            // 첫 번째 API가 두 번째 API의 결과를 사용
            var firstResult = await firstApiHelper.PostAsync<ApiResponse<string>>(
                "/finalize",
                new { processedData = secondResult.Data }
            );

            // Assert
            secondResult.Should().NotBeNull();
            secondResult.Success.Should().BeTrue();
            secondResult.Data.Should().Be("processed-data");

            firstResult.Should().NotBeNull();
            firstResult.Success.Should().BeTrue();
            firstResult.Data.Should().Be("final-result");

            _httpTest.ShouldHaveCalled("https://api.second.com/process")
                .WithVerb(HttpMethod.Post)
                .Times(1);

            _httpTest.ShouldHaveCalled("https://api.first.com/finalize")
                .WithVerb(HttpMethod.Post)
                .Times(1);
        }

        [Fact]
        public async Task EmailApiScenario_SendEmailWithExternalValidation_ShouldSucceed()
        {
            // Arrange
            // 시나리오: 이메일 API가 외부 검증 API를 호출한 후 이메일 전송
            
            // 1. 이메일 검증 API 응답
            var validationResponse = new ApiResponse<bool>
            {
                Success = true,
                Message = "Email validated",
                Data = true,
                StatusCode = 200
            };

            // 2. 이메일 전송 API 응답
            var emailResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "Email sent",
                Data = "email-id-12345",
                StatusCode = 200
            };

            _httpTest.RespondWithJson(validationResponse, 200);
            _httpTest.RespondWithJson(emailResponse, 200);

            // Act
            var validationHelper = new FlurlHttpHelper("https://validation.api.com");
            var emailHelper = new FlurlHttpHelper("https://email.api.com");

            // 1단계: 이메일 주소 검증
            var validation = await validationHelper.PostAsync<ApiResponse<bool>>(
                "/validate",
                new { email = "test@example.com" }
            );

            // 2단계: 검증 성공 시 이메일 전송
            ApiResponse<string>? emailResult = null;
            if (validation.Data)
            {
                emailResult = await emailHelper.PostAsync<ApiResponse<string>>(
                    "/send",
                    new 
                    { 
                        to = "test@example.com",
                        subject = "Test Email",
                        body = "This is a test"
                    }
                );
            }

            // Assert
            validation.Should().NotBeNull();
            validation.Success.Should().BeTrue();
            validation.Data.Should().BeTrue();

            emailResult.Should().NotBeNull();
            emailResult!.Success.Should().BeTrue();
            emailResult.Data.Should().Be("email-id-12345");

            _httpTest.ShouldHaveCalled("https://validation.api.com/validate")
                .Times(1);

            _httpTest.ShouldHaveCalled("https://email.api.com/send")
                .Times(1);
        }

        [Fact]
        public async Task FileUploadChain_UploadToStorageThenNotifyApi_ShouldSucceed()
        {
            // Arrange
            // 시나리오: 파일을 스토리지에 업로드한 후 알림 API 호출
            
            var uploadResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "File uploaded",
                Data = "file-url-https://storage.com/file.txt",
                StatusCode = 200
            };

            var notificationResponse = new ApiResponse<bool>
            {
                Success = true,
                Message = "Notification sent",
                Data = true,
                StatusCode = 200
            };

            _httpTest.RespondWithJson(uploadResponse, 200);
            _httpTest.RespondWithJson(notificationResponse, 200);

            // Act
            var storageHelper = new FlurlHttpHelper("https://storage.api.com");
            var notificationHelper = new FlurlHttpHelper("https://notification.api.com");

            // 1단계: 파일 업로드
            using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("test file content"));
            var uploadResult = await storageHelper.PostWithFileAsync<ApiResponse<string>>(
                "/upload",
                fileStream,
                "test.txt",
                "file"
            );

            // 2단계: 업로드 완료 알림
            var notificationResult = await notificationHelper.PostAsync<ApiResponse<bool>>(
                "/notify",
                new 
                { 
                    fileUrl = uploadResult.Data,
                    uploadedAt = DateTime.UtcNow
                }
            );

            // Assert
            uploadResult.Should().NotBeNull();
            uploadResult.Success.Should().BeTrue();
            uploadResult.Data.Should().Contain("file-url-");

            notificationResult.Should().NotBeNull();
            notificationResult.Success.Should().BeTrue();
            notificationResult.Data.Should().BeTrue();

            _httpTest.ShouldHaveCalled("https://storage.api.com/upload")
                .WithVerb(HttpMethod.Post)
                .Times(1);

            _httpTest.ShouldHaveCalled("https://notification.api.com/notify")
                .WithVerb(HttpMethod.Post)
                .Times(1);
        }

        [Fact]
        public async Task MultipleFilesWithMetadata_UploadAndProcess_ShouldSucceed()
        {
            // Arrange
            // 시나리오: 여러 파일을 업로드하고 메타데이터 처리 API 호출
            
            var uploadResponse = new ApiResponse<List<string>>
            {
                Success = true,
                Message = "Files uploaded",
                Data = new List<string> { "file1-id", "file2-id" },
                StatusCode = 200
            };

            var processingResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "Processing started",
                Data = "job-id-789",
                StatusCode = 200
            };

            _httpTest.RespondWithJson(uploadResponse, 200);
            _httpTest.RespondWithJson(processingResponse, 200);

            // Act
            var uploadHelper = new FlurlHttpHelper("https://upload.api.com");
            var processingHelper = new FlurlHttpHelper("https://processing.api.com");

            // 1단계: 다중 파일 업로드
            var files = new List<FileUploadInfo>
            {
                new FileUploadInfo("files", new MemoryStream(Encoding.UTF8.GetBytes("content1")), "file1.txt"),
                new FileUploadInfo("files", new MemoryStream(Encoding.UTF8.GetBytes("content2")), "file2.txt")
            };

            try
            {
                var uploadResult = await uploadHelper.PostWithFilesAsync<ApiResponse<List<string>>>(
                    "/upload-multiple",
                    files,
                    new Dictionary<string, string> 
                    { 
                        { "userId", "user123" },
                        { "category", "documents" }
                    }
                );

                // 2단계: 업로드된 파일들에 대한 처리 작업 시작
                var processingResult = await processingHelper.PostAsync<ApiResponse<string>>(
                    "/start-processing",
                    new 
                    { 
                        fileIds = uploadResult.Data,
                        processingType = "ocr"
                    }
                );

                // Assert
                uploadResult.Should().NotBeNull();
                uploadResult.Success.Should().BeTrue();
                uploadResult.Data.Should().HaveCount(2);

                processingResult.Should().NotBeNull();
                processingResult.Success.Should().BeTrue();
                processingResult.Data.Should().StartWith("job-id-");

                _httpTest.ShouldHaveCalled("https://upload.api.com/upload-multiple")
                    .WithVerb(HttpMethod.Post)
                    .Times(1);

                _httpTest.ShouldHaveCalled("https://processing.api.com/start-processing")
                    .WithVerb(HttpMethod.Post)
                    .Times(1);
            }
            finally
            {
                foreach (var file in files)
                {
                    file.Dispose();
                }
            }
        }

        [Fact]
        public async Task GetAfterPost_CreateResourceThenRetrieve_ShouldSucceed()
        {
            // Arrange
            // 시나리오: POST로 리소스 생성 후 GET으로 조회
            
            var createResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "Resource created",
                Data = "resource-id-456",
                StatusCode = 201
            };

            var getResponse = new ApiResponse<object>
            {
                Success = true,
                Message = "Resource found",
                Data = new 
                { 
                    id = "resource-id-456",
                    name = "Test Resource",
                    createdAt = DateTime.UtcNow
                },
                StatusCode = 200
            };

            _httpTest.RespondWithJson(createResponse, 201);
            _httpTest.RespondWithJson(getResponse, 200);

            // Act
            var apiHelper = new FlurlHttpHelper("https://api.test.com");

            // 1단계: 리소스 생성
            var createResult = await apiHelper.PostAsync<ApiResponse<string>>(
                "/resources",
                new { name = "Test Resource", type = "document" }
            );

            // 2단계: 생성된 리소스 조회
            var getResult = await apiHelper.GetAsync<ApiResponse<object>>(
                $"/resources/{createResult.Data}",
                new Dictionary<string, string> { { "Authorization", "Bearer token" } }
            );

            // Assert
            createResult.Should().NotBeNull();
            createResult.Success.Should().BeTrue();
            createResult.Data.Should().Contain("resource-id-");

            getResult.Should().NotBeNull();
            getResult.Success.Should().BeTrue();
            getResult.Data.Should().NotBeNull();

            _httpTest.ShouldHaveCalled("https://api.test.com/resources")
                .WithVerb(HttpMethod.Post)
                .Times(1);

            _httpTest.ShouldHaveCalled($"https://api.test.com/resources/{createResult.Data}")
                .WithVerb(HttpMethod.Get)
                .Times(1);
        }

        [Fact]
        public async Task ErrorHandling_FirstApiFailsSecondApiSkipped_ShouldHandleGracefully()
        {
            // Arrange
            // 시나리오: 첫 번째 API 실패 시 두 번째 API 호출하지 않음
            
            var errorResponse = new ApiResponse<string>
            {
                Success = false,
                Message = "Validation failed",
                Data = null,
                StatusCode = 400
            };

            _httpTest.RespondWithJson(errorResponse, 400);

            // Act
            var validationHelper = new FlurlHttpHelper("https://validation.api.com");
            var emailHelper = new FlurlHttpHelper("https://email.api.com");

            ApiResponse<string>? validationResult = null;
            try
            {
                validationResult = await validationHelper.PostAsync<ApiResponse<string>>(
                    "/validate",
                    new { email = "invalid-email" }
                );
            }
            catch (Flurl.Http.FlurlHttpException ex)
            {
                // HTTP 400 오류를 정상적으로 처리
                validationResult = new ApiResponse<string>
                {
                    Success = false,
                    Message = "Validation failed",
                    Data = null,
                    StatusCode = (int)ex.StatusCode!.Value
                };
            }

            // 검증 실패 시 이메일 전송하지 않음
            ApiResponse<string>? emailResult = null;
            if (validationResult?.Success == true)
            {
                emailResult = await emailHelper.PostAsync<ApiResponse<string>>(
                    "/send",
                    new { to = "invalid-email" }
                );
            }

            // Assert
            validationResult.Should().NotBeNull();
            validationResult!.Success.Should().BeFalse();
            validationResult.Message.Should().Be("Validation failed");

            emailResult.Should().BeNull();

            _httpTest.ShouldHaveCalled("https://validation.api.com/validate")
                .Times(1);

            _httpTest.ShouldNotHaveCalled("https://email.api.com/send");
        }
    }
}
