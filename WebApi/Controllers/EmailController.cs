using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers
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
}
