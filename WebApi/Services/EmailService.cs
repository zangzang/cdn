using WebApi.Models;
using HttpClientHelper.Core;

namespace WebApi.Services
{
    public class EmailService : IEmailService
    {
        private readonly FlurlHttpHelper _httpHelper;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(
            ILogger<EmailService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // 외부 이메일 API 설정 (appsettings.json에서 읽기)
            var emailApiBaseUrl = configuration["EmailApi:BaseUrl"] ?? "https://api.email-service.com";
            var defaultTimeout = configuration.GetValue<int>("EmailApi:TimeoutSeconds", 60);

            _httpHelper = new FlurlHttpHelper(emailApiBaseUrl, defaultTimeout);
        }

        /// <summary>
        /// 이메일 전송
        /// </summary>
        public async Task<EmailSendResponse> SendEmailAsync(EmailSendRequest request)
        {
            try
            {
                // 헤더 설정
                var headers = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_configuration["EmailApi:ApiKey"]}" },
                    { "X-Client-Id", _configuration["EmailApi:ClientId"] ?? "default-client" }
                };

                // 쿼리 파라미터 설정
                var queryParams = new Dictionary<string, object>
                {
                    { "priority", request.Priority.ToLower() },
                    { "track_opens", true },
                    { "track_clicks", true }
                };

                // 첨부파일이 있는 경우
                if (request.Attachments != null && request.Attachments.Any())
                {
                    return await SendEmailWithAttachmentsAsync(request, headers, queryParams);
                }
                // 첨부파일이 없는 경우
                else
                {
                    return await SendEmailWithoutAttachmentsAsync(request, headers, queryParams);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "이메일 전송 실패");
                return new EmailSendResponse
                {
                    Success = false,
                    Message = $"이메일 전송 실패: {ex.Message}",
                    SentAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// 첨부파일 포함 이메일 전송
        /// </summary>
        private async Task<EmailSendResponse> SendEmailWithAttachmentsAsync(
            EmailSendRequest request,
            Dictionary<string, string> headers,
            Dictionary<string, object> queryParams)
        {
            // 폼 데이터 준비
            var formData = new Dictionary<string, string>
            {
                { "recipients", request.Recipients },
                { "subject", request.Subject },
                { "body", request.Body },
                { "is_html", request.IsHtml.ToString().ToLower() }
            };

            // Cc, Bcc 추가
            if (!string.IsNullOrWhiteSpace(request.Cc))
                formData.Add("cc", request.Cc);

            if (!string.IsNullOrWhiteSpace(request.Bcc))
                formData.Add("bcc", request.Bcc);

            // FileUploadInfo 리스트 생성
            var files = new List<FileUploadInfo>();

            try
            {
                foreach (var attachment in request.Attachments!)
                {
                    // IFormFile을 Stream으로 변환
                    var memoryStream = new MemoryStream();
                    await attachment.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    files.Add(new FileUploadInfo(
                        fieldName: "attachments",
                        stream: memoryStream,
                        fileName: attachment.FileName
                    ));
                }

                // FlurlHttpHelper를 사용하여 API 호출
                var apiResponse = await _httpHelper.PostWithFilesAsync<ApiResponse<EmailApiResponse>>(
                    endpoint: "/v1/emails/send",
                    files: files,
                    formData: formData,
                    headers: headers,
                    queryParams: queryParams,
                    timeoutSeconds: 120
                );

                return MapToEmailSendResponse(apiResponse, request.Attachments.Count);
            }
            finally
            {
                // Stream 정리
                foreach (var file in files)
                {
                    file.Dispose();
                }
            }
        }

        /// <summary>
        /// 첨부파일 없는 이메일 전송
        /// </summary>
        private async Task<EmailSendResponse> SendEmailWithoutAttachmentsAsync(
            EmailSendRequest request,
            Dictionary<string, string> headers,
            Dictionary<string, object> queryParams)
        {
            var data = new
            {
                recipients = request.Recipients.Split(',').Select(r => r.Trim()).ToList(),
                cc = request.Cc?.Split(',').Select(r => r.Trim()).ToList(),
                bcc = request.Bcc?.Split(',').Select(r => r.Trim()).ToList(),
                subject = request.Subject,
                body = request.Body,
                is_html = request.IsHtml
            };

            // FlurlHttpHelper를 사용하여 API 호출
            var apiResponse = await _httpHelper.PostAsync<ApiResponse<EmailApiResponse>>(
                endpoint: "/v1/emails/send",
                data: data,
                headers: headers,
                queryParams: queryParams,
                timeoutSeconds: 60
            );

            return MapToEmailSendResponse(apiResponse, 0);
        }

        /// <summary>
        /// 이메일 상태 조회
        /// </summary>
        public async Task<EmailStatusResponse> GetEmailStatusAsync(string messageId)
        {
            try
            {
                var headers = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_configuration["EmailApi:ApiKey"]}" }
                };

                var apiResponse = await _httpHelper.GetAsync<ApiResponse<EmailStatusApiResponse>>(
                    endpoint: $"/v1/emails/{messageId}/status",
                    headers: headers,
                    timeoutSeconds: 30
                );

                return new EmailStatusResponse
                {
                    MessageId = apiResponse.Data?.MessageId ?? string.Empty,
                    Status = apiResponse.Data?.Status ?? string.Empty,
                    SentAt = apiResponse.Data?.SentAt ?? DateTime.MinValue,
                    DeliveredAt = apiResponse.Data?.DeliveredAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "이메일 상태 조회 실패 - MessageId: {MessageId}", messageId);
                throw;
            }
        }

        /// <summary>
        /// API 응답을 EmailSendResponse로 변환
        /// </summary>
        private EmailSendResponse MapToEmailSendResponse(
            ApiResponse<EmailApiResponse> apiResponse,
            int attachmentCount)
        {
            return new EmailSendResponse
            {
                Success = apiResponse.Success,
                Message = apiResponse.Message ?? string.Empty,
                MessageId = apiResponse.Data?.MessageId,
                SentAt = apiResponse.Data?.SentAt ?? DateTime.UtcNow,
                AttachmentCount = attachmentCount
            };
        }
    }
}
