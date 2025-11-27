using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace YourNamespace.TestClient
{
    /// <summary>
    /// Email API 테스트 클라이언트
    /// </summary>
    public class EmailApiTestClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public EmailApiTestClient(string baseUrl = "https://localhost:7001")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromMinutes(2)
            };
        }

        #region 테스트 메소드

        /// <summary>
        /// 테스트 1: 파일 첨부 없는 간단한 이메일
        /// </summary>
        public async Task Test1_SendSimpleEmail()
        {
            Console.WriteLine("=== 테스트 1: 간단한 이메일 전송 ===");

            var request = new
            {
                recipients = "user@example.com",
                subject = "테스트 이메일",
                body = "안녕하세요. 테스트 이메일입니다.",
                isHtml = false
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/email/send-simple", content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Response: {result}");
            Console.WriteLine();
        }

        /// <summary>
        /// 테스트 2: 단일 파일 첨부
        /// </summary>
        public async Task Test2_SendEmailWithSingleFile()
        {
            Console.WriteLine("=== 테스트 2: 단일 파일 첨부 ===");

            using var content = new MultipartFormDataContent();

            // 폼 데이터 추가
            content.Add(new StringContent("user@example.com"), "recipients");
            content.Add(new StringContent("파일 첨부 테스트"), "subject");
            content.Add(new StringContent("첨부파일을 확인해주세요."), "body");
            content.Add(new StringContent("false"), "isHtml");
            content.Add(new StringContent("Normal"), "priority");

            // 파일 추가 (테스트용 텍스트 파일 생성)
            var fileContent = new ByteArrayContent(
                Encoding.UTF8.GetBytes("This is a test file content.")
            );
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            content.Add(fileContent, "attachments", "test-document.txt");

            var response = await _httpClient.PostAsync("/api/email/send", content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Response: {result}");
            Console.WriteLine();
        }

        /// <summary>
        /// 테스트 3: 다중 파일 첨부
        /// </summary>
        public async Task Test3_SendEmailWithMultipleFiles()
        {
            Console.WriteLine("=== 테스트 3: 다중 파일 첨부 ===");

            using var content = new MultipartFormDataContent();

            // 폼 데이터
            content.Add(new StringContent("user1@example.com,user2@example.com"), "recipients");
            content.Add(new StringContent("admin@example.com"), "cc");
            content.Add(new StringContent("다중 파일 첨부 테스트"), "subject");
            content.Add(new StringContent("<h1>첨부파일이 여러 개입니다</h1><p>확인 부탁드립니다.</p>"), "body");
            content.Add(new StringContent("true"), "isHtml");
            content.Add(new StringContent("High"), "priority");

            // 파일 1: 텍스트 파일
            var file1Content = new ByteArrayContent(
                Encoding.UTF8.GetBytes("First file content")
            );
            file1Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            content.Add(file1Content, "attachments", "file1.txt");

            // 파일 2: JSON 파일
            var file2Content = new ByteArrayContent(
                Encoding.UTF8.GetBytes("{\"test\": \"data\"}")
            );
            file2Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            content.Add(file2Content, "attachments", "data.json");

            // 파일 3: CSV 파일
            var file3Content = new ByteArrayContent(
                Encoding.UTF8.GetBytes("Name,Age\nJohn,30\nJane,25")
            );
            file3Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(file3Content, "attachments", "users.csv");

            var response = await _httpClient.PostAsync("/api/email/send", content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Response: {result}");
            Console.WriteLine();
        }

        /// <summary>
        /// 테스트 4: 실제 파일 첨부 (파일 시스템에서 읽기)
        /// </summary>
        public async Task Test4_SendEmailWithRealFile(string filePath)
        {
            Console.WriteLine("=== 테스트 4: 실제 파일 첨부 ===");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"파일을 찾을 수 없습니다: {filePath}");
                return;
            }

            using var content = new MultipartFormDataContent();

            // 폼 데이터
            content.Add(new StringContent("recipient@example.com"), "recipients");
            content.Add(new StringContent("실제 파일 첨부"), "subject");
            content.Add(new StringContent("파일을 첨부했습니다."), "body");
            content.Add(new StringContent("false"), "isHtml");

            // 파일 읽기 및 추가
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileContent = new ByteArrayContent(fileBytes);
            
            var fileName = Path.GetFileName(filePath);
            var mimeType = GetMimeType(fileName);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
            
            content.Add(fileContent, "attachments", fileName);

            var response = await _httpClient.PostAsync("/api/email/send", content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"File: {fileName} ({fileBytes.Length} bytes)");
            Console.WriteLine($"Response: {result}");
            Console.WriteLine();
        }

        /// <summary>
        /// 테스트 5: 이메일 상태 조회
        /// </summary>
        public async Task Test5_GetEmailStatus(string messageId)
        {
            Console.WriteLine("=== 테스트 5: 이메일 상태 조회 ===");

            var response = await _httpClient.GetAsync($"/api/email/status/{messageId}");
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Response: {result}");
            Console.WriteLine();
        }

        /// <summary>
        /// 테스트 6: 입력 검증 테스트 (잘못된 이메일)
        /// </summary>
        public async Task Test6_ValidationTest()
        {
            Console.WriteLine("=== 테스트 6: 입력 검증 (잘못된 이메일) ===");

            using var content = new MultipartFormDataContent();

            // 잘못된 이메일 형식
            content.Add(new StringContent("invalid-email"), "recipients");
            content.Add(new StringContent(""), "subject"); // 빈 제목
            content.Add(new StringContent("Test body"), "body");

            var response = await _httpClient.PostAsync("/api/email/send", content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Response: {result}");
            Console.WriteLine();
        }

        #endregion

        #region Helper 메소드

        /// <summary>
        /// MIME 타입 추론
        /// </summary>
        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }

    /// <summary>
    /// 프로그램 진입점
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Email API 테스트 클라이언트");
            Console.WriteLine("================================\n");

            var client = new EmailApiTestClient("https://localhost:7001");

            try
            {
                // 테스트 1: 간단한 이메일
                await client.Test1_SendSimpleEmail();

                // 테스트 2: 단일 파일 첨부
                await client.Test2_SendEmailWithSingleFile();

                // 테스트 3: 다중 파일 첨부
                await client.Test3_SendEmailWithMultipleFiles();

                // 테스트 4: 실제 파일 첨부 (경로 지정 필요)
                // await client.Test4_SendEmailWithRealFile(@"C:\Documents\test.pdf");

                // 테스트 5: 상태 조회 (MessageId 필요)
                // await client.Test5_GetEmailStatus("message-id-here");

                // 테스트 6: 입력 검증
                await client.Test6_ValidationTest();

                Console.WriteLine("\n모든 테스트 완료!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n오류 발생: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine("\n계속하려면 아무 키나 누르세요...");
            Console.ReadKey();
        }
    }
}


/* ========================================
   Postman / cURL 예제
   ======================================== */

/*
// cURL - 간단한 이메일 전송
curl -X POST "https://localhost:7001/api/email/send-simple" \
  -H "Content-Type: application/json" \
  -d '{
    "recipients": "user@example.com",
    "subject": "테스트",
    "body": "내용",
    "isHtml": false
  }'

// cURL - 파일 첨부
curl -X POST "https://localhost:7001/api/email/send" \
  -F "recipients=user@example.com" \
  -F "subject=파일 첨부" \
  -F "body=첨부파일 확인" \
  -F "isHtml=false" \
  -F "attachments=@/path/to/file.pdf" \
  -F "attachments=@/path/to/image.jpg"

// cURL - 상태 조회
curl -X GET "https://localhost:7001/api/email/status/{messageId}"
*/