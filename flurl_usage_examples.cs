using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YourNamespace.Helpers;

namespace YourNamespace.Examples
{
    public class FlurlHttpHelperExamples
    {
        private readonly FlurlHttpHelper _httpHelper;

        public FlurlHttpHelperExamples()
        {
            // Helper 초기화
            _httpHelper = new FlurlHttpHelper("https://api.example.com", defaultTimeoutSeconds: 60);
        }

        #region 예제 1: 파일 없는 기본 POST 요청

        public async Task Example1_BasicPost()
        {
            var data = new
            {
                Name = "홍길동",
                Age = 30,
                Email = "hong@example.com"
            };

            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer YOUR_TOKEN" },
                { "X-Custom-Header", "CustomValue" }
            };

            var queryParams = new Dictionary<string, object>
            {
                { "api_version", "v2" },
                { "compress", true }
            };

            var response = await _httpHelper.PostAsync<ApiResponse<UserResponse>>(
                endpoint: "/users",
                data: data,
                headers: headers,
                queryParams: queryParams,
                timeoutSeconds: 30
            );

            Console.WriteLine($"Success: {response.Success}, Data: {response.Data.UserId}");
        }

        #endregion

        #region 예제 2: 폼 데이터 POST

        public async Task Example2_FormPost()
        {
            var formData = new Dictionary<string, string>
            {
                { "username", "user123" },
                { "password", "pass123" },
                { "remember_me", "true" }
            };

            var response = await _httpHelper.PostFormAsync<ApiResponse<LoginResponse>>(
                endpoint: "/auth/login",
                formData: formData
            );

            Console.WriteLine($"Token: {response.Data.AccessToken}");
        }

        #endregion

        #region 예제 3: 단일 파일 업로드 (파일 경로)

        public async Task Example3_SingleFileUpload()
        {
            var filePath = @"C:\Documents\report.pdf";

            var formData = new Dictionary<string, string>
            {
                { "title", "Q4 보고서" },
                { "description", "2024년 4분기 실적" },
                { "category", "reports" }
            };

            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer YOUR_TOKEN" }
            };

            var queryParams = new Dictionary<string, object>
            {
                { "folder", "documents" },
                { "overwrite", true }
            };

            var response = await _httpHelper.PostWithFileAsync<ApiResponse<FileUploadResponse>>(
                endpoint: "/files/upload",
                filePath: filePath,
                fieldName: "file",
                formData: formData,
                headers: headers,
                queryParams: queryParams,
                timeoutSeconds: 120
            );

            Console.WriteLine($"파일 업로드 성공: {response.Data.FileId}");
        }

        #endregion

        #region 예제 4: 단일 파일 업로드 (Stream)

        public async Task Example4_SingleFileUploadWithStream()
        {
            using var fileStream = File.OpenRead(@"C:\Images\photo.jpg");

            var formData = new Dictionary<string, string>
            {
                { "alt_text", "풍경 사진" },
                { "tags", "nature,travel" }
            };

            var queryParams = new Dictionary<string, object>
            {
                { "resize", true },
                { "width", 800 },
                { "height", 600 }
            };

            var response = await _httpHelper.PostWithFileAsync<ApiResponse<ImageUploadResponse>>(
                endpoint: "/images/upload",
                fileStream: fileStream,
                fileName: "photo.jpg",
                fieldName: "image",
                formData: formData,
                queryParams: queryParams
            );

            Console.WriteLine($"이미지 URL: {response.Data.ImageUrl}");
        }

        #endregion

        #region 예제 5: 단일 파일 업로드 (byte 배열)

        public async Task Example5_SingleFileUploadWithBytes()
        {
            byte[] fileBytes = File.ReadAllBytes(@"C:\Data\data.csv");

            var response = await _httpHelper.PostWithFileAsync<ApiResponse<FileUploadResponse>>(
                endpoint: "/data/import",
                fileBytes: fileBytes,
                fileName: "data.csv",
                fieldName: "datafile",
                formData: new Dictionary<string, string>
                {
                    { "delimiter", "," },
                    { "has_header", "true" }
                }
            );

            Console.WriteLine($"Import 완료: {response.Data.RecordsProcessed} rows");
        }

        #endregion

        #region 예제 6: 다중 파일 업로드 (같은 필드명)

        public async Task Example6_MultipleFilesUpload()
        {
            var filePaths = new List<string>
            {
                @"C:\Images\photo1.jpg",
                @"C:\Images\photo2.jpg",
                @"C:\Images\photo3.jpg"
            };

            var formData = new Dictionary<string, string>
            {
                { "album_name", "여행 사진" },
                { "visibility", "public" }
            };

            var queryParams = new Dictionary<string, object>
            {
                { "generate_thumbnails", true },
                { "max_width", 1920 }
            };

            var response = await _httpHelper.PostWithFilesAsync<ApiResponse<BatchUploadResponse>>(
                endpoint: "/images/batch-upload",
                filePaths: filePaths,
                fieldName: "images",
                formData: formData,
                queryParams: queryParams,
                timeoutSeconds: 180
            );

            Console.WriteLine($"{response.Data.SuccessCount}개 파일 업로드 성공");
        }

        #endregion

        #region 예제 7: 다중 파일 업로드 (서로 다른 필드명)

        public async Task Example7_MultipleFilesDifferentFields()
        {
            var filesWithFieldNames = new Dictionary<string, string>
            {
                { "document", @"C:\Documents\contract.pdf" },
                { "signature", @"C:\Images\signature.png" },
                { "attachment", @"C:\Documents\terms.docx" }
            };

            var formData = new Dictionary<string, string>
            {
                { "contract_type", "employment" },
                { "effective_date", "2024-01-01" }
            };

            var response = await _httpHelper.PostWithMultipleFilesAsync<ApiResponse<ContractUploadResponse>>(
                endpoint: "/contracts/submit",
                filePathsWithFieldNames: filesWithFieldNames,
                formData: formData
            );

            Console.WriteLine($"계약서 제출 완료: {response.Data.ContractId}");
        }

        #endregion

        #region 예제 8: FileUploadInfo 객체 사용

        public async Task Example8_FileUploadInfoUsage()
        {
            var files = new List<FileUploadInfo>
            {
                new FileUploadInfo("profile_image", @"C:\Images\profile.jpg"),
                new FileUploadInfo("cover_image", @"C:\Images\cover.jpg"),
                new FileUploadInfo("resume", File.OpenRead(@"C:\Documents\resume.pdf"), "resume.pdf")
            };

            try
            {
                var response = await _httpHelper.PostWithFilesAsync<ApiResponse<ProfileUploadResponse>>(
                    endpoint: "/profile/update",
                    files: files,
                    formData: new Dictionary<string, string>
                    {
                        { "bio", "Software Developer" },
                        { "location", "Seoul, Korea" }
                    }
                );

                Console.WriteLine($"프로필 업데이트 성공: {response.Data.ProfileUrl}");
            }
            finally
            {
                // Stream 해제
                foreach (var file in files)
                {
                    file.Dispose();
                }
            }
        }

        #endregion

        #region 예제 9: GET 요청

        public async Task Example9_GetRequest()
        {
            var queryParams = new Dictionary<string, object>
            {
                { "page", 1 },
                { "limit", 10 },
                { "sort", "created_desc" }
            };

            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer YOUR_TOKEN" }
            };

            var response = await _httpHelper.GetAsync<ApiResponse<List<UserResponse>>>(
                endpoint: "/users",
                headers: headers,
                queryParams: queryParams
            );

            Console.WriteLine($"Total users: {response.Data.Count}");
        }

        #endregion

        #region 예제 10: 에러 핸들링

        public async Task Example10_ErrorHandling()
        {
            try
            {
                var response = await _httpHelper.PostWithFileAsync<ApiResponse<FileUploadResponse>>(
                    endpoint: "/files/upload",
                    filePath: @"C:\Documents\file.pdf",
                    timeoutSeconds: 30
                );

                if (response.Success)
                {
                    Console.WriteLine("업로드 성공!");
                }
                else
                {
                    Console.WriteLine($"업로드 실패: {response.Message}");
                }
            }
            catch (Flurl.Http.FlurlHttpException ex)
            {
                var statusCode = ex.StatusCode;
                var errorResponse = await ex.GetResponseStringAsync();
                Console.WriteLine($"HTTP 에러 [{statusCode}]: {errorResponse}");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"파일 없음: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("요청 타임아웃");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"예상치 못한 에러: {ex.Message}");
            }
        }

        #endregion

        #region 응답 모델 예시

        public class UserResponse
        {
            public string UserId { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
        }

        public class LoginResponse
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        public class FileUploadResponse
        {
            public string FileId { get; set; }
            public string FileName { get; set; }
            public long FileSize { get; set; }
            public string Url { get; set; }
            public int RecordsProcessed { get; set; }
        }

        public class ImageUploadResponse
        {
            public string ImageId { get; set; }
            public string ImageUrl { get; set; }
            public string ThumbnailUrl { get; set; }
        }

        public class BatchUploadResponse
        {
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public List<string> UploadedFileIds { get; set; }
        }

        public class ContractUploadResponse
        {
            public string ContractId { get; set; }
            public string Status { get; set; }
            public DateTime SubmittedAt { get; set; }
        }

        public class ProfileUploadResponse
        {
            public string ProfileUrl { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        #endregion
    }
}