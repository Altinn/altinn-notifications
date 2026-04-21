using System.Text.Json;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Core.Status;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Core.Sending;

public class SendingServiceTests
{
    [Fact]
    public async Task SendAsync_InvalidEmailAddress_DispatchesStatusResult()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Email.Core.Sending.Email email =
        new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_InvalidEmailFormat });

        Mock<IEmailStatusCheckDispatcher> sendingAcceptedPublisherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        statusDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);
        Mock<IEmailServiceRateLimitDispatcher> emailServiceRateLimitDispatcherMock = new();

        var sendingService = new SendingService(clientMock.Object, sendingAcceptedPublisherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendAsync(email);

        // Assert
        statusDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == id &&
                r.OperationId == string.Empty &&
                r.SendResult == EmailSendResult.Failed_InvalidEmailFormat)),
            Times.Once);

        statusDispatcherMock.VerifyNoOtherCalls();
        sendingAcceptedPublisherMock.VerifyNoOtherCalls();
        emailServiceRateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_OperationIdentifierGenerated_DelegatedToSendingAcceptedPublisher()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Email.Core.Sending.Email email =
        new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .ReturnsAsync("operation-id");

        Mock<IEmailStatusCheckDispatcher> sendingAcceptedPublisherMock = new();
        sendingAcceptedPublisherMock
            .Setup(p => p.DispatchAsync(id, "operation-id"))
            .Returns(Task.CompletedTask);

        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        Mock<IEmailServiceRateLimitDispatcher> emailServiceRateLimitDispatcherMock = new();

        var sendingService = new SendingService(clientMock.Object, sendingAcceptedPublisherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendAsync(email);

        // Assert
        sendingAcceptedPublisherMock.Verify(p => p.DispatchAsync(id, "operation-id"), Times.Once);

        statusDispatcherMock.VerifyNoOtherCalls();
        sendingAcceptedPublisherMock.VerifyNoOtherCalls();
        emailServiceRateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_Failed_TransientError_DispatchesEmailServiceRateLimitAndStatusResult()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Email.Core.Sending.Email email =
        new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_TransientError, IntermittentErrorDelay = 1000 });

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        statusDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);

        Mock<IEmailServiceRateLimitDispatcher> emailServiceRateLimitDispatcherMock = new();
        emailServiceRateLimitDispatcherMock.Setup(d => d.DispatchAsync(
            It.Is<GenericServiceUpdate>(u =>
                u.Source == "platform-notifications-email" &&
                u.Schema == AltinnServiceUpdateSchema.ResourceLimitExceeded)))
            .Returns(Task.CompletedTask);

        var testStart = DateTime.UtcNow;

        var sendingService = new SendingService(clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendAsync(email);

        // Assert - rate limit signal goes via dispatcher with correct payload
        emailServiceRateLimitDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<GenericServiceUpdate>(u =>
                u.Source == "platform-notifications-email" &&
                u.Schema == AltinnServiceUpdateSchema.ResourceLimitExceeded &&
                VerifyRateLimitData(u.Data, testStart, delaySeconds: 1000))),
            Times.Once);

        // Assert - status result goes via dispatcher
        statusDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == id &&
                r.OperationId == string.Empty &&
                r.SendResult == EmailSendResult.Failed_TransientError)),
            Times.Once);

        checkDispatcherMock.VerifyNoOtherCalls();
        statusDispatcherMock.VerifyNoOtherCalls();
        emailServiceRateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_Quarantined)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    [InlineData(EmailSendResult.Failed_SupressedRecipient)]
    public async Task SendAsync_NonTransientFailure_DispatchesStatusResultWithCorrectValues(EmailSendResult failResult)
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Email.Core.Sending.Email email =
        new(id, "test", "body", "fromAddress", "toAddress", EmailContentType.Plain);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendEmail(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .ReturnsAsync(new EmailClientErrorResponse { SendResult = failResult });

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        statusDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);
        Mock<IEmailServiceRateLimitDispatcher> emailServiceRateLimitDispatcherMock = new();

        var sendingService = new SendingService(clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendAsync(email);

        // Assert
        statusDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == id &&
                r.OperationId == string.Empty &&
                r.SendResult == failResult)),
            Times.Once);

        checkDispatcherMock.VerifyNoOtherCalls();
        statusDispatcherMock.VerifyNoOtherCalls();
        emailServiceRateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    private static bool VerifyRateLimitData(string json, DateTime testStart, int delaySeconds)
    {
        var data = JsonSerializer.Deserialize<ResourceLimitExceeded>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return data != null &&
               data.Resource == "azure-communication-services-email" &&
               data.ResetTime >= testStart.AddSeconds(delaySeconds) &&
               data.ResetTime <= testStart.AddSeconds(delaySeconds + 5);
    }
}
