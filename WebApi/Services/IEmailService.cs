using WebApi.Models;

namespace WebApi.Services
{
    public interface IEmailService
    {
        Task<EmailSendResponse> SendEmailAsync(EmailSendRequest request);
        Task<EmailStatusResponse> GetEmailStatusAsync(string messageId);
    }
}
