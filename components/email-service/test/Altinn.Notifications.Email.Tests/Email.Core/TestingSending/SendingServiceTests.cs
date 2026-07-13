using System.Text.Json;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Exceptions;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

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

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, sendingAcceptedPublisherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

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

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, sendingAcceptedPublisherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

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

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

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

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, emailServiceRateLimitDispatcherMock.Object);

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

    [Fact]
    public async Task SendComposedAsync_Success_DispatchesStatusCheckWithOperationIdAndTotalAttachmentSizeBytes()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        const string operationId = "composed-op-id";
        const long encodedSize = 204800L;
        var attachment = new SasFileAttachment { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/report.pdf?sv=2024" };
        var email = new ComposedEmail(id, "subject", "body", "from@test.no", "to@test.no", EmailContentType.Plain, [attachment]);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendComposedEmail(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComposedEmailSendResult { OperationId = operationId, TotalAttachmentSizeBytes = encodedSize });

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        checkDispatcherMock
            .Setup(d => d.DispatchAsync(id, operationId, encodedSize))
            .Returns(Task.CompletedTask);

        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        Mock<IEmailServiceRateLimitDispatcher> rateLimitDispatcherMock = new();

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, rateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendComposedAsync(email, TestContext.Current.CancellationToken);

        // Assert
        checkDispatcherMock.Verify(d => d.DispatchAsync(id, operationId, encodedSize), Times.Once);

        statusDispatcherMock.VerifyNoOtherCalls();
        rateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendComposedAsync_PayloadTooLarge_DispatchesStatusResultWithTotalAttachmentSizeBytes()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        const long encodedSize = 1_048_576L;
        var attachment = new SasFileAttachment { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/report.pdf?sv=2024" };
        var email = new ComposedEmail(id, "subject", "body", "from@test.no", "to@test.no", EmailContentType.Plain, [attachment]);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendComposedEmail(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailClientErrorResponse
            {
                SendResult = EmailSendResult.Failed_PayloadTooLarge,
                TotalAttachmentSizeBytes = encodedSize
            });

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        statusDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);
        Mock<IEmailServiceRateLimitDispatcher> rateLimitDispatcherMock = new();

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, rateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendComposedAsync(email, TestContext.Current.CancellationToken);

        // Assert
        statusDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == id &&
                r.SendResult == EmailSendResult.Failed_PayloadTooLarge &&
                r.TotalAttachmentSizeBytes == encodedSize)),
            Times.Once);

        checkDispatcherMock.VerifyNoOtherCalls();
        statusDispatcherMock.VerifyNoOtherCalls();
        rateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendComposedAsync_AcsNonTransientFailure_DispatchesStatusResult()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        var attachment = new SasFileAttachment { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/report.pdf?sv=2024" };
        var email = new ComposedEmail(id, "subject", "body", "from@test.no", "to@test.no", EmailContentType.Plain, [attachment]);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendComposedEmail(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_PayloadTooLarge });

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        statusDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);
        Mock<IEmailServiceRateLimitDispatcher> rateLimitDispatcherMock = new();

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, rateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendComposedAsync(email, TestContext.Current.CancellationToken);

        // Assert
        statusDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == id &&
                r.SendResult == EmailSendResult.Failed_PayloadTooLarge)),
            Times.Once);

        checkDispatcherMock.VerifyNoOtherCalls();
        statusDispatcherMock.VerifyNoOtherCalls();
        rateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendComposedAsync_AcsTransientFailure_DispatchesRateLimitAndStatusResult()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        var attachment = new SasFileAttachment { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/report.pdf?sv=2024" };
        var email = new ComposedEmail(id, "subject", "body", "from@test.no", "to@test.no", EmailContentType.Plain, [attachment]);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendComposedEmail(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailClientErrorResponse { SendResult = EmailSendResult.Failed_TransientError, IntermittentErrorDelay = 60 });

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        statusDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);
        Mock<IEmailServiceRateLimitDispatcher> rateLimitDispatcherMock = new();
        rateLimitDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<GenericServiceUpdate>()))
            .Returns(Task.CompletedTask);

        var testStart = DateTime.UtcNow;
        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, rateLimitDispatcherMock.Object);

        // Act
        await sendingService.SendComposedAsync(email, TestContext.Current.CancellationToken);
        var testEnd = DateTime.UtcNow;

        // Assert
        rateLimitDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<GenericServiceUpdate>(u =>
                u.Source == "platform-notifications-email" &&
                u.Schema == AltinnServiceUpdateSchema.ResourceLimitExceeded &&
                VerifyRateLimitData(u.Data, testStart, testEnd, delaySeconds: 60))),
            Times.Once);

        statusDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == id &&
                r.SendResult == EmailSendResult.Failed_TransientError)),
            Times.Once);

        checkDispatcherMock.VerifyNoOtherCalls();
        statusDispatcherMock.VerifyNoOtherCalls();
        rateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendComposedAsync_InvalidSasUrlException_LogsWarningAndDispatchesFailedInvalidSasUrl()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        var attachment = new SasFileAttachment { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/report.pdf?sv=2024" };
        var email = new ComposedEmail(id, "subject", "body", "from@test.no", "to@test.no", EmailContentType.Plain, [attachment]);
        var exception = new InvalidSasUrlException("attachment.pdf", 403);

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendComposedEmail(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        statusDispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Returns(Task.CompletedTask);
        Mock<IEmailServiceRateLimitDispatcher> rateLimitDispatcherMock = new();

        Mock<ILogger<SendingService>> loggerMock = new();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var sendingService = new SendingService(loggerMock.Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, rateLimitDispatcherMock.Object);

        // Act — must complete without throwing so the ASB message is acknowledged
        await sendingService.SendComposedAsync(email, TestContext.Current.CancellationToken);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(id.ToString())),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        statusDispatcherMock.Verify(
            d => d.DispatchAsync(It.Is<SendOperationResult>(r =>
                r.NotificationId == id &&
                r.SendResult == EmailSendResult.Failed_InvalidSasUrl)),
            Times.Once);

        checkDispatcherMock.VerifyNoOtherCalls();
        rateLimitDispatcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendComposedAsync_AttachmentDownloadException_PropagatesWithoutDispatching()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        var attachment = new SasFileAttachment { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/report.pdf?sv=2024" };
        var email = new ComposedEmail(id, "subject", "body", "from@test.no", "to@test.no", EmailContentType.Plain, [attachment]);
        var exception = new AttachmentDownloadException("report.pdf", Guid.NewGuid(), 503, new HttpRequestException("transient"));

        Mock<IEmailServiceClient> clientMock = new();
        clientMock.Setup(c => c.SendComposedEmail(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        Mock<IEmailStatusCheckDispatcher> checkDispatcherMock = new();
        Mock<IEmailSendResultDispatcher> statusDispatcherMock = new();
        Mock<IEmailServiceRateLimitDispatcher> rateLimitDispatcherMock = new();

        var sendingService = new SendingService(new Mock<ILogger<SendingService>>().Object, clientMock.Object, checkDispatcherMock.Object, statusDispatcherMock.Object, rateLimitDispatcherMock.Object);

        // Act — AttachmentDownloadException must propagate so Wolverine can apply the retry policy
        var thrown = await Assert.ThrowsAsync<AttachmentDownloadException>(
            () => sendingService.SendComposedAsync(email, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(exception, thrown);

        checkDispatcherMock.VerifyNoOtherCalls();
        statusDispatcherMock.VerifyNoOtherCalls();
        rateLimitDispatcherMock.VerifyNoOtherCalls();
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
