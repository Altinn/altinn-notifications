using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Integrations.Wolverine.Handlers;

using Microsoft.Extensions.Logging;

using Moq;

using ISendingService = Altinn.Notifications.Sms.Core.Sending.ISendingService;
using SmsMessage = Altinn.Notifications.Sms.Core.Sending.Sms;

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

    /// <summary>
    /// Verifies that a general <see cref="Exception"/> thrown by the sending service
    /// is logged at error level and then rethrown.
    /// </summary>
    [Fact]
    public async Task HandleAsync_GeneralException_LogsErrorAndRethrows()
    {
        // Arrange
        var exception = new InvalidOperationException("SMS gateway unavailable");

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<SmsMessage>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendSmsCommandHandler.HandleAsync(_validSendSmsCommand, sendingServiceMock.Object, loggerMock.Object));

        Assert.Same(exception, thrown);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send SMS") && v.ToString()!.Contains(_validSendSmsCommand.NotificationId.ToString())),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that an <see cref="OperationCanceledException"/> thrown by the sending service
    /// is rethrown directly without invoking the send-failure error logger.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OperationCanceledException_RethrowsWithoutLoggingException()
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<SmsMessage>()))
            .ThrowsAsync(new OperationCanceledException());

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SendSmsCommandHandler.HandleAsync(_validSendSmsCommand, sendingServiceMock.Object, loggerMock.Object));

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send SMS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
