using FluentAssertions;
using Moq;
using WebApi.Models;
using WebApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Tests
{
    public class EmailServiceTests
    {
        private readonly Mock<ILogger<EmailService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;

        public EmailServiceTests()
        {
            _loggerMock = new Mock<ILogger<EmailService>>();
            _configurationMock = new Mock<IConfiguration>();

            // Configuration 설정
            _configurationMock.Setup(c => c["EmailApi:BaseUrl"]).Returns("https://test-api.email-service.com");
            _configurationMock.Setup(c => c["EmailApi:ApiKey"]).Returns("test-api-key");
            _configurationMock.Setup(c => c["EmailApi:ClientId"]).Returns("test-client");
            _configurationMock.Setup(c => c.GetSection("EmailApi:TimeoutSeconds").Value).Returns("60");
        }

        [Fact]
        public void EmailSendRequest_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var request = new EmailSendRequest();

            // Assert
            request.Recipients.Should().BeEmpty();
            request.Subject.Should().BeEmpty();
            request.Body.Should().BeEmpty();
            request.IsHtml.Should().BeFalse();
            request.Priority.Should().Be("Normal");
            request.Attachments.Should().BeNull();
        }

        [Fact]
        public void EmailSendResponse_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var response = new EmailSendResponse();

            // Assert
            response.Success.Should().BeFalse();
            response.Message.Should().BeEmpty();
            response.MessageId.Should().BeNull();
            response.AttachmentCount.Should().Be(0);
        }

        [Fact]
        public void SimpleEmailRequest_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var request = new SimpleEmailRequest();

            // Assert
            request.Recipients.Should().BeEmpty();
            request.Subject.Should().BeEmpty();
            request.Body.Should().BeEmpty();
            request.IsHtml.Should().BeFalse();
            request.Cc.Should().BeNull();
            request.Bcc.Should().BeNull();
        }

        [Fact]
        public void ErrorResponse_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var response = new ErrorResponse
            {
                Success = false,
                Message = "Test error",
                Errors = new List<string> { "Error 1", "Error 2" }
            };

            // Assert
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Test error");
            response.Errors.Should().HaveCount(2);
            response.Errors.Should().Contain("Error 1");
            response.Errors.Should().Contain("Error 2");
        }

        [Theory]
        [InlineData("test@example.com")]
        [InlineData("user@domain.co.kr")]
        [InlineData("name.surname@company.org")]
        public void EmailSendRequest_ValidEmails_ShouldBeAccepted(string email)
        {
            // Arrange
            var request = new EmailSendRequest
            {
                Recipients = email,
                Subject = "Test Subject",
                Body = "Test Body"
            };

            // Assert
            request.Recipients.Should().Be(email);
        }

        [Fact]
        public void EmailStatusResponse_ShouldHaveCorrectProperties()
        {
            // Arrange
            var sentAt = DateTime.UtcNow;
            var deliveredAt = DateTime.UtcNow.AddMinutes(1);

            // Act
            var response = new EmailStatusResponse
            {
                MessageId = "msg-123",
                Status = "delivered",
                SentAt = sentAt,
                DeliveredAt = deliveredAt
            };

            // Assert
            response.MessageId.Should().Be("msg-123");
            response.Status.Should().Be("delivered");
            response.SentAt.Should().Be(sentAt);
            response.DeliveredAt.Should().Be(deliveredAt);
        }
    }
}
