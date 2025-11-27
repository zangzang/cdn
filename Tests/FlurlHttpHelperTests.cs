using FluentAssertions;
using HttpClientHelper.Core;
using Xunit;
using Flurl.Http.Testing;

namespace Tests
{
    public class FlurlHttpHelperTests : IDisposable
    {
        private readonly HttpTest _httpTest;

        public FlurlHttpHelperTests()
        {
            _httpTest = new HttpTest();
        }

        public void Dispose()
        {
            _httpTest?.Dispose();
        }

        #region 기본 초기화 테스트

        [Fact]
        public void FlurlHttpHelper_ShouldInitializeWithBaseUrl()
        {
            // Arrange & Act
            var helper = new FlurlHttpHelper("https://api.example.com");

            // Assert
            helper.Should().NotBeNull();
        }

        [Fact]
        public void FlurlHttpHelper_ShouldInitializeWithCustomTimeout()
        {
            // Arrange & Act
            var helper = new FlurlHttpHelper("https://api.example.com", 120);

            // Assert
            helper.Should().NotBeNull();
        }

        #endregion

        #region POST 요청 테스트

        [Fact]
        public async Task PostAsync_ShouldSendJsonDataAndReturnResponse()
        {
            // Arrange
            var expectedResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "Success",
                Data = "Test Data",
                StatusCode = 200
            };

            _httpTest.RespondWithJson(expectedResponse);

            var helper = new FlurlHttpHelper("https://api.test.com");
            var requestData = new { Name = "Test", Value = 123 };

            // Act
            var result = await helper.PostAsync<ApiResponse<string>>(
                "/api/test",
                requestData,
                new Dictionary<string, string> { { "Authorization", "Bearer token" } },
                new Dictionary<string, object> { { "param1", "value1" } }
            );

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be("Success");
            result.Data.Should().Be("Test Data");

            _httpTest.ShouldHaveCalled("https://api.test.com/api/test")
                .WithVerb(HttpMethod.Post)
                .WithHeader("Authorization", "Bearer token")
                .WithQueryParam("param1", "value1")
                .Times(1);
        }

        [Fact]
        public async Task PostFormAsync_ShouldSendFormDataAndReturnResponse()
        {
            // Arrange
            var expectedResponse = new ApiResponse<bool>
            {
                Success = true,
                Message = "Form submitted",
                Data = true,
                StatusCode = 200
            };

            _httpTest.RespondWithJson(expectedResponse);

            var helper = new FlurlHttpHelper("https://api.test.com");
            var formData = new Dictionary<string, string>
            {
                { "username", "testuser" },
                { "email", "test@example.com" }
            };

            // Act
            var result = await helper.PostFormAsync<ApiResponse<bool>>(
                "/api/form",
                formData
            );

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().BeTrue();

            _httpTest.ShouldHaveCalled("https://api.test.com/api/form")
                .WithVerb(HttpMethod.Post)
                .Times(1);
        }

        [Fact]
        public async Task PostAsStringAsync_ShouldReturnStringResponse()
        {
            // Arrange
            _httpTest.RespondWith("Success Response");

            var helper = new FlurlHttpHelper("https://api.test.com");
            var requestData = new { Test = "Data" };

            // Act
            var result = await helper.PostAsStringAsync("/api/string", requestData);

            // Assert
            result.Should().Be("Success Response");

            _httpTest.ShouldHaveCalled("https://api.test.com/api/string")
                .WithVerb(HttpMethod.Post)
                .Times(1);
        }

        #endregion

        #region GET 요청 테스트

        [Fact]
        public async Task GetAsync_ShouldReturnResponse()
        {
            // Arrange
            var expectedResponse = new ApiResponse<List<string>>
            {
                Success = true,
                Message = "Retrieved",
                Data = new List<string> { "Item1", "Item2", "Item3" },
                StatusCode = 200
            };

            _httpTest.RespondWithJson(expectedResponse);

            var helper = new FlurlHttpHelper("https://api.test.com");

            // Act
            var result = await helper.GetAsync<ApiResponse<List<string>>>(
                "/api/items",
                new Dictionary<string, string> { { "Authorization", "Bearer token" } }
            );

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().HaveCount(3);

            _httpTest.ShouldHaveCalled("https://api.test.com/api/items")
                .WithVerb(HttpMethod.Get)
                .Times(1);
        }

        #endregion

        #region 파일 업로드 테스트

        [Fact]
        public async Task PostWithFileAsync_WithStream_ShouldUploadFile()
        {
            // Arrange
            var expectedResponse = new ApiResponse<string>
            {
                Success = true,
                Message = "File uploaded",
                Data = "file-id-123",
                StatusCode = 200
            };

            _httpTest.RespondWithJson(expectedResponse);

            var helper = new FlurlHttpHelper("https://api.test.com");
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

            // Act
            var result = await helper.PostWithFileAsync<ApiResponse<string>>(
                "/api/upload",
                stream,
                "test.txt",
                "file"
            );

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().Be("file-id-123");

            _httpTest.ShouldHaveCalled("https://api.test.com/api/upload")
                .WithVerb(HttpMethod.Post)
                .Times(1);
        }

        [Fact]
        public async Task PostWithFilesAsync_WithFileUploadInfo_ShouldUploadMultipleFiles()
        {
            // Arrange
            var expectedResponse = new ApiResponse<int>
            {
                Success = true,
                Message = "Files uploaded",
                Data = 2,
                StatusCode = 200
            };

            _httpTest.RespondWithJson(expectedResponse);

            var helper = new FlurlHttpHelper("https://api.test.com");
            
            var files = new List<FileUploadInfo>
            {
                new FileUploadInfo("files", new MemoryStream(new byte[] { 1, 2, 3 }), "file1.txt"),
                new FileUploadInfo("files", new MemoryStream(new byte[] { 4, 5, 6 }), "file2.txt")
            };

            try
            {
                // Act
                var result = await helper.PostWithFilesAsync<ApiResponse<int>>(
                    "/api/upload-multiple",
                    files,
                    new Dictionary<string, string> { { "description", "Test files" } }
                );

                // Assert
                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.Data.Should().Be(2);

                _httpTest.ShouldHaveCalled("https://api.test.com/api/upload-multiple")
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

        #endregion

        #region FileUploadInfo 테스트

        [Fact]
        public void FileUploadInfo_ShouldInitializeWithStreamConstructor()
        {
            // Arrange
            using var stream = new MemoryStream();

            // Act
            var fileInfo = new FileUploadInfo("testField", stream, "test.txt");

            // Assert
            fileInfo.FieldName.Should().Be("testField");
            fileInfo.FileName.Should().Be("test.txt");
            fileInfo.Stream.Should().BeSameAs(stream);
        }

        [Fact]
        public void FileUploadInfo_ShouldThrowWhenFileNotFound()
        {
            // Arrange & Act & Assert
            var action = () => new FileUploadInfo("testField", "nonexistent-file.txt");
            action.Should().Throw<FileNotFoundException>();
        }

        [Fact]
        public void FileUploadInfo_ShouldDisposeStream()
        {
            // Arrange
            var stream = new MemoryStream();
            var fileInfo = new FileUploadInfo("testField", stream, "test.txt");

            // Act
            fileInfo.Dispose();

            // Assert
            var action = () => stream.ReadByte();
            action.Should().Throw<ObjectDisposedException>();
        }

        #endregion

        #region ApiResponse 테스트

        [Fact]
        public void ApiResponse_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var response = new ApiResponse<string>();

            // Assert
            response.Success.Should().BeFalse();
            response.Message.Should().BeNull();
            response.Data.Should().BeNull();
            response.StatusCode.Should().Be(0);
        }

        [Fact]
        public void ApiResponse_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var response = new ApiResponse<string>
            {
                Success = true,
                Message = "OK",
                Data = "Test Data",
                StatusCode = 200
            };

            // Assert
            response.Success.Should().BeTrue();
            response.Message.Should().Be("OK");
            response.Data.Should().Be("Test Data");
            response.StatusCode.Should().Be(200);
        }

        #endregion
    }
}
