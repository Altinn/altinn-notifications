using Altinn.Notifications.Shared.Commands;

using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Integrations.Wolverine.Handlers;

using Microsoft.Extensions.Logging;

using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations.Wolverine;

public class SendSmsCommandHandlerTests
{
    private readonly SendSmsCommand _validSendSmsCommand = new()
    {
        NotificationId = Guid.NewGuid(),
        MobileNumber = "+4799999999",
        Body = "Test message body",
        SenderNumber = "Altinn"
    };

    [Fact]
    public async Task HandleAsync_GeneralException_LogsWarningAndRethrows()
    {
        // Arrange
        var exception = new InvalidOperationException("SMS gateway unavailable");

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendSmsCommandHandler.HandleAsync(_validSendSmsCommand, sendingServiceMock.Object, loggerMock.Object));

        Assert.Same(exception, thrown);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send SMS") && v.ToString()!.Contains(_validSendSmsCommand.NotificationId.ToString())),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmptyNotificationId_LogsErrorAndDiscardsMessage()
    {
        // Arrange
        var command = new SendSmsCommand { NotificationId = Guid.Empty };

        var sendingServiceMock = new Mock<ISendingService>();

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act
        await SendSmsCommandHandler.HandleAsync(command, sendingServiceMock.Object, loggerMock.Object);

        // Assert
        sendingServiceMock.Verify(s => s.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()), Times.Never);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("missing NotificationId")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OperationCanceledException_LogsWarningAndRethrows()
    {
        // Arrange
        var exception = new OperationCanceledException();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<Notifications.Sms.Core.Sending.Sms>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SendSmsCommandHandler.HandleAsync(_validSendSmsCommand, sendingServiceMock.Object, loggerMock.Object));

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send SMS")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
