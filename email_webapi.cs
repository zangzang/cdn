using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly ILogger<EmailController> _logger;
        private readonly IEmailService _emailService;

        public EmailController(ILogger<EmailController> logger, IEmailService emailService)
        {
            _logger = logger;
            _emailService = emailService;
        }

        /// <summary>
        /// 이메일 전송 (파일 첨부 포함)
        /// </summary>
        /// <param name="request">이메일 전송 요청</param>
        /// <returns>전송 결과</returns>
        [HttpPost("send")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(EmailSendResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendEmail([FromForm] EmailSendRequest request)
        {
            try
            {
                _logger.LogInformation("이메일 전송 요청 - 수신자: {Recipients}, 제목: {Subject}", 
                    request.Recipients, request.Subject);

                // 입력 검증
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Success = false,
                        Message = "입력값이 올바르지 않습니다.",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                // 이메일 전송
                var result = await _emailService.SendEmailAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("이메일 전송 성공 - MessageId: {MessageId}", result.MessageId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("이메일 전송 실패 - {Message}", result.Message);
                    return BadRequest(new ErrorResponse
                    {
                        Success = false,
                        Message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "이메일 전송 중 오류 발생");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Success = false,
                    Message = "이메일 전송 중 오류가 발생했습니다.",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 이메일 전송 (JSON 바디, 파일 없음)
        /// </summary>
        [HttpPost("send-simple")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(EmailSendResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> SendSimpleEmail([FromBody] SimpleEmailRequest request)
        {
            try
            {
                _logger.LogInformation("간단 이메일 전송 요청 - 수신자: {Recipients}", request.Recipients);

                var emailRequest = new EmailSendRequest
                {
                    Recipients = request.Recipients,
                    Subject = request.Subject,
                    Body = request.Body,
                    Cc = request.Cc,
                    Bcc = request.Bcc,
                    IsHtml = request.IsHtml
                };

                var result = await _emailService.SendEmailAsync(emailRequest);

                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "간단 이메일 전송 중 오류 발생");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// 이메일 전송 상태 조회
        /// </summary>
        [HttpGet("status/{messageId}")]
        [ProducesResponseType(typeof(EmailStatusResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEmailStatus(string messageId)
        {
            try
            {
                var status = await _emailService.GetEmailStatusAsync(messageId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "이메일 상태 조회 중 오류 발생");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }

    #region Request Models

    /// <summary>
    /// 이메일 전송 요청 (파일 첨부 포함)
    /// </summary>
    public class EmailSendRequest
    {
        /// <summary>
        /// 수신자 이메일 (쉼표로 구분)
        /// </summary>
        [Required(ErrorMessage = "수신자는 필수입니다.")]
        [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
        public string Recipients { get; set; } = string.Empty;

        /// <summary>
        /// 참조 (Cc)
        /// </summary>
        public string? Cc { get; set; }

        /// <summary>
        /// 숨은참조 (Bcc)
        /// </summary>
        public string? Bcc { get; set; }

        /// <summary>
        /// 이메일 제목
        /// </summary>
        [Required(ErrorMessage = "제목은 필수입니다.")]
        [StringLength(200, ErrorMessage = "제목은 200자를 초과할 수 없습니다.")]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// 이메일 내용
        /// </summary>
        [Required(ErrorMessage = "내용은 필수입니다.")]
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// HTML 형식 여부
        /// </summary>
        public bool IsHtml { get; set; } = false;

        /// <summary>
        /// 첨부 파일 목록
        /// </summary>
        public List<IFormFile>? Attachments { get; set; }

        /// <summary>
        /// 우선순위 (Low, Normal, High)
        /// </summary>
        public string Priority { get; set; } = "Normal";
    }

    /// <summary>
    /// 간단한 이메일 전송 요청 (파일 없음)
    /// </summary>
    public class SimpleEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Recipients { get; set; } = string.Empty;

        public string? Cc { get; set; }
        public string? Bcc { get; set; }

        [Required]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public bool IsHtml { get; set; } = false;
    }

    #endregion

    #region Response Models

    /// <summary>
    /// 이메일 전송 응답
    /// </summary>
    public class EmailSendResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MessageId { get; set; }
        public DateTime SentAt { get; set; }
        public int AttachmentCount { get; set; }
    }

    /// <summary>
    /// 이메일 상태 응답
    /// </summary>
    public class EmailStatusResponse
    {
        public string MessageId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    /// <summary>
    /// 에러 응답
    /// </summary>
    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? Errors { get; set; }
    }

    #endregion
}