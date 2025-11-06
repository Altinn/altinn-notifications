using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Clients;
using Altinn.Notifications.Email.Integrations.Configuration;

using Azure;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations
{
    public class EmailServiceClientTests
    {
        [Theory]
        [InlineData("PerSubscriptionPerHourLimitExceeded - Please try again after 3636 seconds.", 3636)]
        [InlineData("PerSubscriptionPerMinuteLimitExceeded - Please try again after 60 seconds.", 60)]
        [InlineData("Random error message not mentioning specific seconds", 60)]
        [InlineData("PerSubscriptionPerHourLimitExceeded - Please try again after 4000 seconds. Status: 429 (Too Many Requests) ErrorCode: TooManyRequests", 4000)]
        public void GetDelayFromString_WithVariousMessages_ReturnsExpectedDelay(string input, int expectedDelay)
        {
            // Arrange
            var communicationServicesSettings = new CommunicationServicesSettings
            {
                ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=testkey"
            };
            var emailServiceAdminSettings = new EmailServiceAdminSettings
            {
                IntermittentErrorDelay = 60
            };
            var loggerMock = new Mock<ILogger<EmailServiceClient>>();
            var client = new EmailServiceClient(communicationServicesSettings, emailServiceAdminSettings, loggerMock.Object);

            // Act
            var result = client.GetDelayFromString(input);

            // Assert
            Assert.Equal(expectedDelay, result);
        }

        [Theory]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(300)]
        public void GetUnknownErrorDelay_WithConfiguredDelay_ReturnsConfiguredValue(int configuredDelay)
        {
            // Arrange
            var communicationServicesSettings = new CommunicationServicesSettings
            {
                ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=testkey"
            };
            var emailServiceAdminSettings = new EmailServiceAdminSettings
            {
                IntermittentErrorDelay = configuredDelay
            };
            var loggerMock = new Mock<ILogger<EmailServiceClient>>();
            var client = new EmailServiceClient(communicationServicesSettings, emailServiceAdminSettings, loggerMock.Object);

            // Act
            var result = client.GetUnknownErrorDelay();

            // Assert
            Assert.Equal(configuredDelay, result);
        }

        [Theory]
        [InlineData(500, EmailSendResult.Failed_TransientError)] // Internal Server Error
        [InlineData(502, EmailSendResult.Failed_TransientError)] // Bad Gateway
        [InlineData(503, EmailSendResult.Failed_TransientError)] // Service Unavailable
        [InlineData(504, EmailSendResult.Failed_TransientError)] // Gateway Timeout
        [InlineData(599, EmailSendResult.Failed_TransientError)] // Edge case - highest 5xx
        [InlineData(400, EmailSendResult.Failed)] // Bad Request - not transient
        [InlineData(404, EmailSendResult.Failed)] // Not Found - not transient
        [InlineData(429, EmailSendResult.Failed)] // Too Many Requests - handled separately by error code, not status
        public void GetEmailSendResult_WithVariousStatusCodes_ReturnsExpectedResult(int statusCode, EmailSendResult expectedResult)
        {
            // Arrange
            var exception = new RequestFailedException(statusCode, "Test error message");

            // Act
            var result = EmailServiceClient.GetEmailSendResult(exception);

            // Assert
            Assert.Equal(expectedResult, result);
        }
    }
}
