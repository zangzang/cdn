using System.ComponentModel.DataAnnotations;

namespace WebApi.Models
{
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

    #region API Response Models

    /// <summary>
    /// 외부 이메일 API 응답
    /// </summary>
    public class EmailApiResponse
    {
        public string MessageId { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// 외부 이메일 API 상태 응답
    /// </summary>
    public class EmailStatusApiResponse
    {
        public string MessageId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? OpenedAt { get; set; }
        public int OpenCount { get; set; }
    }

    #endregion
}
