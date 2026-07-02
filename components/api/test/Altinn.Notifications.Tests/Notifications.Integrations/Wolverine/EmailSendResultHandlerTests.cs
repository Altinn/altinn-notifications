using System;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Wolverine.Handlers;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public sealed class EmailSendResultHandlerTests
{
    private readonly Mock<IEmailNotificationService> _serviceMock;

    public EmailSendResultHandlerTests()
    {
        _serviceMock = new Mock<IEmailNotificationService>();
    }

    [Fact]
    public async Task Handle_UnrecognizedSendResult_ThrowsUnrecognizedSendResultException()
    {
        // Arrange
        var command = new EmailSendResultCommand
        {
            NotificationId = Guid.NewGuid(),
            OperationId = "op-123",
            SendResult = "TOTALLY_UNKNOWN_VALUE"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnrecognizedSendResultException>(
            () => EmailSendResultHandler.Handle(command, _serviceMock.Object, NullLogger.Instance));

        Assert.Equal("TOTALLY_UNKNOWN_VALUE", exception.SendResult);
    }

    [Fact]
    public async Task Handle_UnrecognizedSendResult_DoesNotCallService()
    {
        // Arrange
        var command = new EmailSendResultCommand
        {
            NotificationId = Guid.NewGuid(),
            SendResult = "NOT_A_VALID_RESULT"
        };

        // Act
        await Assert.ThrowsAsync<UnrecognizedSendResultException>(
            () => EmailSendResultHandler.Handle(command, _serviceMock.Object, NullLogger.Instance));

        // Assert
        _serviceMock.Verify(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()), Times.Never);
    }

    [Theory]
    [InlineData(EmailNotificationResultType.Failed)]
    [InlineData(EmailNotificationResultType.Sending)]
    [InlineData(EmailNotificationResultType.Succeeded)]
    [InlineData(EmailNotificationResultType.Failed_InvalidSasUrl)]
    [InlineData(EmailNotificationResultType.Failed_PayloadTooLarge)]
    public async Task Handle_RecognizedSendResult_CallsUpdateSendStatus(EmailNotificationResultType resultType)
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var command = new EmailSendResultCommand
        {
            NotificationId = notificationId,
            OperationId = "op-456",
            SendResult = resultType.ToString()
        };

        _serviceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Returns(Task.CompletedTask);

        // Act
        await EmailSendResultHandler.Handle(command, _serviceMock.Object, NullLogger.Instance);

        // Assert
        _serviceMock.Verify(
            s => s.UpdateSendStatus(It.Is<EmailSendOperationResult>(r =>
                r.NotificationId == notificationId &&
                r.SendResult == resultType &&
                r.OperationId == "op-456")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ComposedEmailResult_MapsEncodedAttachmentsSizeToOperationResult()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        const long encodedSize = 204800L;
        var command = new EmailSendResultCommand
        {
            OperationId = "op-789",
            NotificationId = notificationId,
            EncodedAttachmentsSize = encodedSize,
            SendResult = EmailNotificationResultType.Succeeded.ToString()
        };

        _serviceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<EmailSendOperationResult>()))
            .Returns(Task.CompletedTask);

        // Act
        await EmailSendResultHandler.Handle(command, _serviceMock.Object, NullLogger.Instance);

        // Assert
        _serviceMock.Verify(
            s => s.UpdateSendStatus(It.Is<EmailSendOperationResult>(r =>
                r.NotificationId == notificationId &&
                r.EncodedAttachmentsSize == encodedSize)),
            Times.Once);
    }
}
