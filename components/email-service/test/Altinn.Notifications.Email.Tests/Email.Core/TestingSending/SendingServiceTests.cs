using System.Collections.Immutable;
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
    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task SendAsync_InvalidEmailAddress_DispatchesStatusResult()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Email.Core.Sending.Email email =
        new(NotificationId: id, Attachments: [], Body: "body", ContentType: EmailContentType.Plain, FromAddress: "fromAddress", Subject: "test", ToAddress: "toAddress");

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
    public async Task SendAsync_WithAttachments_PassesEmailWithAttachmentsToClient()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        var attachments = ImmutableList.Create(new EmailAttachment("file.pdf", "application/pdf", "dGVzdA=="));

        Notifications.Email.Core.Sending.Email email =
        new(NotificationId: id, Attachments: attachments, Body: "body", ContentType: EmailContentType.Plain, FromAddress: "fromAddress", Subject: "test", ToAddress: "toAddress");

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendEmail(It.Is<Notifications.Email.Core.Sending.Email>(e => e.Attachments == attachments)))
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
        clientMock.Verify(c => c.SendEmail(It.Is<Notifications.Email.Core.Sending.Email>(e => e.Attachments == attachments)), Times.Once);
        sendingAcceptedPublisherMock.Verify(p => p.DispatchAsync(id, "operation-id"), Times.Once);

        statusDispatcherMock.VerifyNoOtherCalls();
        emailServiceRateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_OperationIdentifierGenerated_DelegatedToSendingAcceptedPublisher()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        Notifications.Email.Core.Sending.Email email =
        new(NotificationId: id, Attachments: [], Body: "body", ContentType: EmailContentType.Plain, FromAddress: "fromAddress", Subject: "test", ToAddress: "toAddress");

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
        new(NotificationId: id, Attachments: [], Body: "body", ContentType: EmailContentType.Plain, FromAddress: "fromAddress", Subject: "test", ToAddress: "toAddress");

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
        var testEnd = DateTime.UtcNow;

        // Assert - rate limit signal goes via dispatcher with correct payload
        emailServiceRateLimitDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<GenericServiceUpdate>(u =>
                u.Source == "platform-notifications-email" &&
                u.Schema == AltinnServiceUpdateSchema.ResourceLimitExceeded &&
                VerifyRateLimitData(u.Data, testStart, testEnd, delaySeconds: 1000))),
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
        new(Attachments: [], Body: "body", ContentType: EmailContentType.Plain, FromAddress: "fromAddress", NotificationId: id, Subject: "test", ToAddress: "toAddress");

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

    private static bool VerifyRateLimitData(string json, DateTime testStart, DateTime testEnd, int delaySeconds)
    {
        var data = JsonSerializer.Deserialize<ResourceLimitExceeded>(json, _caseInsensitiveOptions);

        return data != null &&
               data.Resource == "azure-communication-services-email" &&
               data.ResetTime >= testStart.AddSeconds(delaySeconds) &&
               data.ResetTime <= testEnd.AddSeconds(delaySeconds);
    }
}
