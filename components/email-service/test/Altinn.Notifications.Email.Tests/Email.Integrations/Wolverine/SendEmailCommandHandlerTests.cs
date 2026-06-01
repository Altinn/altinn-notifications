using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Wolverine.Handlers;
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

    [Fact]
    public async Task HandleAsync_GeneralException_LogsWarningAndRethrows()
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
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send email") && v.ToString()!.Contains(_validSendEmailCommand.NotificationId.ToString())),
                (Exception?)null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

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

    [Fact]
    public async Task HandleAsync_OperationCanceledException_LogsWarningAndRethrows()
    {
        // Arrange
        var exception = new OperationCanceledException();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendAsync(It.IsAny<Notifications.Email.Core.Sending.Email>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SendEmailCommandHandler.HandleAsync(_validSendEmailCommand, sendingServiceMock.Object, loggerMock.Object));

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send email")),
                (Exception?)null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
