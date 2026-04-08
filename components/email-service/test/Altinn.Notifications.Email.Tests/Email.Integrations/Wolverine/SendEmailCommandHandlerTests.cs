using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Wolverine;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations.Wolverine;

public class SendEmailCommandHandlerTests
{
    private readonly SendEmailCommand _validSendEmailCommand = new()
    {
        Body = "Test body",
        ContentType = "Plain",
        Subject = "Test subject",
        NotificationId = Guid.NewGuid(),
        FromAddress = "sender@example.com",
        ToAddress = "recipient@example.com"
    };

    /// <summary>
    /// Verifies that a general <see cref="Exception"/> thrown by
    /// <see cref="ISendingService.SendAsync"/> is logged at error level and then rethrown.
    /// </summary>
    [Fact]
    public async Task HandleAsync_GeneralException_LogsErrorAndRethrows()
    {
        // Arrange
        var exception = new InvalidOperationException("SMTP connection failed");

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendEmailCommandHandler.HandleAsync(_validSendEmailCommand, sendingServiceMock.Object, loggerMock.Object));

        Assert.Same(exception, thrown);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send email") && v.ToString()!.Contains(_validSendEmailCommand.NotificationId.ToString())),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that an unknown <see cref="SendEmailCommand.ContentType"/> value is
    /// logged at error level and the email is sent with <see cref="EmailContentType.Plain"/> as the fallback.
    /// </summary>
    [Fact]
    public async Task HandleAsync_UnknownContentType_LogsErrorAndDefaultsToPlain()
    {
        // Arrange
        var command = _validSendEmailCommand with { ContentType = "UnknownFormat" };

        Notifications.Email.Core.Sending.Email? capturedEmail = null;
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .Callback<Notifications.Email.Core.Sending.Email>(e => capturedEmail = e)
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act
        await SendEmailCommandHandler.HandleAsync(command, sendingServiceMock.Object, loggerMock.Object);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("unknown ContentType") && v.ToString()!.Contains(command.NotificationId.ToString())),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "An unknown ContentType must be logged at error level.");

        Assert.NotNull(capturedEmail);
        Assert.Equal(EmailContentType.Plain, capturedEmail!.ContentType);
    }

    /// <summary>
    /// Verifies that an <see cref="OperationCanceledException"/> thrown by <see cref="ISendingService.SendAsync"/> is rethrown directly without invoking the send-failure error logger.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OperationCanceledException_RethrowsWithoutLoggingException()
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .ThrowsAsync(new OperationCanceledException());

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SendEmailCommandHandler.HandleAsync(_validSendEmailCommand, sendingServiceMock.Object, loggerMock.Object));

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
