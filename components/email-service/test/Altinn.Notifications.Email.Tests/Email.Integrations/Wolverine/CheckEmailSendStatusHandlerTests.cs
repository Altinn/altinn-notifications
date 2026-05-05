using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations.Wolverine;

public class CheckEmailSendStatusHandlerTests
{
    private static CheckEmailSendStatusCommand ValidCommand(Guid? notificationId = null, string operationId = "op-123") =>
        new()
        {
            SendOperationId = operationId,
            LastCheckedAtUtc = DateTime.UtcNow,
            NotificationId = notificationId ?? Guid.NewGuid()
        };

    [Fact]
    public async Task Handle_EmptyNotificationId_ThrowsArgumentException()
    {
        var command = ValidCommand(notificationId: Guid.Empty);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CheckEmailSendStatusHandler.Handle(
                command,
                Mock.Of<IDateTimeService>(),
                Mock.Of<IMessageContext>(),
                Mock.Of<IEmailServiceClient>(),
                Mock.Of<IEmailSendResultDispatcher>()));

        Assert.Equal("checkEmailSendStatusCommand", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceSendOperationId_ThrowsArgumentException(string operationId)
    {
        var command = ValidCommand(operationId: operationId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CheckEmailSendStatusHandler.Handle(
                command,
                Mock.Of<IDateTimeService>(),
                Mock.Of<IMessageContext>(),
                Mock.Of<IEmailServiceClient>(),
                Mock.Of<IEmailSendResultDispatcher>()));

        Assert.Equal("checkEmailSendStatusCommand", ex.ParamName);
    }

    [Theory]
    [InlineData(EmailSendResult.Failed)]
    [InlineData(EmailSendResult.Delivered)]
    [InlineData(EmailSendResult.Failed_Bounced)]
    [InlineData(EmailSendResult.Failed_Quarantined)]
    [InlineData(EmailSendResult.Failed_FilteredSpam)]
    [InlineData(EmailSendResult.Failed_TransientError)]
    [InlineData(EmailSendResult.Failed_InvalidEmailFormat)]
    [InlineData(EmailSendResult.Failed_SupressedRecipient)]
    public async Task Handle_TerminalSendResult_DispatchesViaStatusDispatcher(EmailSendResult terminalResult)
    {
        // Arrange
        var command = ValidCommand();

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(terminalResult);

        SendOperationResult? capturedResult = null;
        var dispatcherMock = new Mock<IEmailSendResultDispatcher>();
        dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>()))
            .Callback<SendOperationResult>(r => capturedResult = r)
            .Returns(Task.CompletedTask);

        // Act
        await CheckEmailSendStatusHandler.Handle(
            command,
            Mock.Of<IDateTimeService>(),
            Mock.Of<IMessageContext>(),
            clientMock.Object,
            dispatcherMock.Object);

        // Assert: terminal result dispatched with correct data
        dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<SendOperationResult>()), Times.Once);
        Assert.NotNull(capturedResult);
        Assert.Equal(command.NotificationId, capturedResult.NotificationId);
        Assert.Equal(command.SendOperationId, capturedResult.OperationId);
        Assert.Equal(terminalResult, capturedResult.SendResult);
    }

    [Fact]
    public async Task Handle_TerminalSendResult_DoesNotScheduleRecheck()
    {
        // Arrange
        var command = ValidCommand();

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(EmailSendResult.Delivered);

        var messageContextMock = new Mock<IMessageContext>();
        var dispatcherMock = new Mock<IEmailSendResultDispatcher>();
        dispatcherMock.Setup(d => d.DispatchAsync(It.IsAny<SendOperationResult>())).Returns(Task.CompletedTask);

        // Act
        await CheckEmailSendStatusHandler.Handle(
            command,
            Mock.Of<IDateTimeService>(),
            messageContextMock.Object,
            clientMock.Object,
            dispatcherMock.Object);

        // Assert
        messageContextMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_SendResultIsSending_SchedulesRecheckAndDoesNotDispatch()
    {
        // Arrange
        var command = ValidCommand();
        var fixedTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var clientMock = new Mock<IEmailServiceClient>();
        clientMock.Setup(c => c.GetOperationUpdate(command.SendOperationId))
            .ReturnsAsync(EmailSendResult.Sending);

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(fixedTime);

        var dispatcherMock = new Mock<IEmailSendResultDispatcher>();
        var messageContextMock = new Mock<IMessageContext>();

        // Act
        await CheckEmailSendStatusHandler.Handle(
            command,
            dateTimeMock.Object,
            messageContextMock.Object,
            clientMock.Object,
            dispatcherMock.Object);

        // Assert
        // ScheduleAsync is a Wolverine extension method that resolves to PublishAsync with DeliveryOptions.ScheduleDelay,
        // so we verify PublishAsync since that is the interface method Moq can intercept.
        messageContextMock.Verify(
            m => m.PublishAsync(
                It.Is<CheckEmailSendStatusCommand>(c =>
                    c.NotificationId == command.NotificationId &&
                    c.SendOperationId == command.SendOperationId &&
                    c.LastCheckedAtUtc == fixedTime),
                It.Is<DeliveryOptions?>(o => o != null && o.ScheduleDelay == TimeSpan.FromMilliseconds(8000))),
            Times.Once);

        dispatcherMock.VerifyNoOtherCalls();
    }
}
