using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Wolverine.Handlers;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations.Wolverine;

public class SendComposedEmailCommandHandlerTests
{
    private readonly SendComposedEmailCommand _validCommand = new()
    {
        NotificationId = Guid.NewGuid(),
        Subject = "Test subject",
        Body = "Test body",
        ContentType = "Plain",
        FromAddress = "sender@example.com",
        ToAddress = "recipient@example.com",
        Attachments =
        [
            new SasFileAttachment { Filename = "doc.pdf", MimeType = "application/pdf", SasUrl = "https://storage.example.com/doc.pdf?sas=token" }
        ]
    };

    [Fact]
    public async Task HandleAsync_ValidCommand_MapsAllFieldsToComposedEmail()
    {
        // Arrange
        ComposedEmail? capturedEmail = null;
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .Callback<ComposedEmail, CancellationToken>((e, _) => capturedEmail = e)
            .Returns(Task.CompletedTask);

        // Act
        await SendComposedEmailCommandHandler.HandleAsync(_validCommand, sendingServiceMock.Object, Mock.Of<ILogger>(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedEmail);
        Assert.Equal(_validCommand.NotificationId, capturedEmail.NotificationId);
        Assert.Equal(_validCommand.Subject, capturedEmail.Subject);
        Assert.Equal(_validCommand.Body, capturedEmail.Body);
        Assert.Equal(_validCommand.FromAddress, capturedEmail.FromAddress);
        Assert.Equal(_validCommand.ToAddress, capturedEmail.ToAddress);
        Assert.Equal(EmailContentType.Plain, capturedEmail.ContentType);
        Assert.Equal(_validCommand.Attachments, capturedEmail.Attachments);
    }

    [Fact]
    public async Task HandleAsync_UnknownContentType_LogsErrorAndDefaultsToPlain()
    {
        // Arrange
        var command = _validCommand with { ContentType = "UnknownFormat" };

        ComposedEmail? capturedEmail = null;
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .Callback<ComposedEmail, CancellationToken>((e, _) => capturedEmail = e)
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act
        await SendComposedEmailCommandHandler.HandleAsync(command, sendingServiceMock.Object, loggerMock.Object, TestContext.Current.CancellationToken);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("unknown ContentType") && v.ToString()!.Contains(command.NotificationId.ToString())),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        Assert.NotNull(capturedEmail);
        Assert.Equal(EmailContentType.Plain, capturedEmail.ContentType);
    }

    [Fact]
    public async Task HandleAsync_GeneralException_LogsWarningAndRethrows()
    {
        // Arrange
        var exception = new InvalidOperationException("ACS error");

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock
            .Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendComposedEmailCommandHandler.HandleAsync(_validCommand, sendingServiceMock.Object, loggerMock.Object, TestContext.Current.CancellationToken));

        Assert.Same(exception, thrown);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("failed to send email") && v.ToString()!.Contains(_validCommand.NotificationId.ToString())),
                (Exception?)null,
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
            .Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SendComposedEmailCommandHandler.HandleAsync(_validCommand, sendingServiceMock.Object, loggerMock.Object, TestContext.Current.CancellationToken));

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
