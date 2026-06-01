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

public sealed class SmsSendResultHandlerTests
{
    private readonly Mock<ISmsNotificationService> _serviceMock;

    public SmsSendResultHandlerTests()
    {
        _serviceMock = new Mock<ISmsNotificationService>();
    }

    [Fact]
    public async Task Handle_UnrecognizedSendResult_ThrowsUnrecognizedSendResultException()
    {
        // Arrange
        var command = new SmsSendResultCommand
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = "gw-123",
            SendResult = "TOTALLY_UNKNOWN_VALUE"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnrecognizedSendResultException>(
            () => SmsSendResultHandler.Handle(command, _serviceMock.Object, NullLogger.Instance));

        Assert.Equal("TOTALLY_UNKNOWN_VALUE", exception.SendResult);
    }

    [Fact]
    public async Task Handle_UnrecognizedSendResult_DoesNotCallService()
    {
        // Arrange
        var command = new SmsSendResultCommand
        {
            NotificationId = Guid.NewGuid(),
            SendResult = "NOT_A_VALID_RESULT"
        };

        // Act
        await Assert.ThrowsAsync<UnrecognizedSendResultException>(
            () => SmsSendResultHandler.Handle(command, _serviceMock.Object, NullLogger.Instance));

        // Assert
        _serviceMock.Verify(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()), Times.Never);
    }

    [Theory]
    [InlineData(SmsNotificationResultType.Accepted)]
    [InlineData(SmsNotificationResultType.Failed)]
    [InlineData(SmsNotificationResultType.Failed_InvalidRecipient)]
    public async Task Handle_RecognizedSendResult_CallsUpdateSendStatus(SmsNotificationResultType resultType)
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var command = new SmsSendResultCommand
        {
            NotificationId = notificationId,
            GatewayReference = "gw-789",
            SendResult = resultType.ToString()
        };

        _serviceMock
            .Setup(s => s.UpdateSendStatus(It.IsAny<SmsSendOperationResult>()))
            .Returns(Task.CompletedTask);

        // Act
        await SmsSendResultHandler.Handle(command, _serviceMock.Object, NullLogger.Instance);

        // Assert
        _serviceMock.Verify(
            s => s.UpdateSendStatus(It.Is<SmsSendOperationResult>(r =>
                r.NotificationId == notificationId &&
                r.SendResult == resultType &&
                r.GatewayReference == "gw-789")),
            Times.Once);
    }
}
