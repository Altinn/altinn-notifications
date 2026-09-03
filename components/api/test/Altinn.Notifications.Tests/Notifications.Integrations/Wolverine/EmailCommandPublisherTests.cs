using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

/// <summary>
/// Unit tests for <see cref="EmailCommandPublisher"/>.
/// </summary>
public class EmailCommandPublisherTests
{
    private readonly Email _email = new(
        Guid.NewGuid(),
        "Test Subject",
        "Test Body",
        "sender@altinnxyz.no",
        "recipient@altinnxyz.no",
        EmailContentType.Html);

    [Fact]
    public async Task PublishAsync_SuccessfulPublish_ReturnsNull()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(_email, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_ReturnsFailedEmail()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(_email, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_email, result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_LogsError()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), TestContext.Current.CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<EmailCommandPublisher>>();
        var publisher = CreatePublisher(messageBusPublisherMock, loggerMock);

        // Act
        await publisher.PublishAsync(_email, TestContext.Current.CancellationToken);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, cts.Token));

        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), TestContext.Current.CancellationToken))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_ValidEmail_MapsAllFieldsCorrectlyToSendEmailCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var email = new Email(notificationId, "Hello", "<p>World</p>", "from@test.no", "to@test.no", EmailContentType.Html);

        SendEmailCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), TestContext.Current.CancellationToken))
            .Callback<SendEmailCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("Hello", capturedCommand.Subject);
        Assert.Equal("<p>World</p>", capturedCommand.Body);
        Assert.Equal("to@test.no", capturedCommand.ToAddress);
        Assert.Equal("from@test.no", capturedCommand.FromAddress);
        Assert.Equal(notificationId, capturedCommand.NotificationId);
        Assert.Equal(EmailContentType.Html.ToString(), capturedCommand.ContentType);
    }

    [Fact]
    public async Task PublishAsync_PlainContentType_MapsContentTypeEnumToString()
    {
        // Arrange
        var email = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Plain);

        SendEmailCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), TestContext.Current.CancellationToken))
            .Callback<SendEmailCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Plain", capturedCommand!.ContentType);
    }

    [Fact]
    public async Task PublishAsync_HtmlContentType_MapsContentTypeEnumToString()
    {
        // Arrange
        var email = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Html);

        SendEmailCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendEmailCommand>(), TestContext.Current.CancellationToken))
            .Callback<SendEmailCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(email, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Html", capturedCommand!.ContentType);
    }

    [Fact]
    public async Task PublishAsync_Batch_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        var email1 = new Email(Guid.NewGuid(), "Subject 1", "Body 1", "from@test.no", "to1@test.no", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "Subject 2", "Body 2", "from@test.no", "to2@test.no", EmailContentType.Plain);
        var emails = new List<Email> { email1, email2 };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: null);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task PublishAsync_Batch_AllFail_ReturnsAllFailedEmails()
    {
        // Arrange
        var email1 = new Email(Guid.NewGuid(), "Subject 1", "Body 1", "from@test.no", "to1@test.no", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "Subject 2", "Body 2", "from@test.no", "to2@test.no", EmailContentType.Plain);
        var emails = new List<Email> { email1, email2 };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: _ => true);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(email1, result);
        Assert.Contains(email2, result);
    }

    [Fact]
    public async Task PublishAsync_Batch_SomeFail_ReturnsOnlyFailedEmails()
    {
        // Arrange
        var successEmail = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "success@test.no", EmailContentType.Plain);
        var failEmail = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "fail@test.no", EmailContentType.Plain);
        var emails = new List<Email> { successEmail, failEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: email => email.NotificationId == failEmail.NotificationId);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Contains(failEmail, result);
        Assert.DoesNotContain(successEmail, result);
    }

    [Fact]
    public async Task PublishAsync_Batch_EmptyList_DelegatesToMessageBusPublisher()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: null);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync([], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusPublisherMock.Verify(
            m => m.PublishBatchAsync(
                It.Is<IReadOnlyList<Email>>(items => items.Count == 0),
                It.IsAny<Func<Email, SendEmailCommand>>(),
                It.IsAny<Action<Email, Exception>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Batch_PassesConfiguredConcurrencyToMessageBusPublisher()
    {
        // Arrange
        const int configuredConcurrency = 7;
        var emails = new List<Email> { _email };

        int? capturedConcurrency = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<Email>>(),
                It.IsAny<Func<Email, SendEmailCommand>>(),
                It.IsAny<Action<Email, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Email>, Func<Email, SendEmailCommand>, Action<Email, Exception>?, CancellationToken>(
                (_, _, onError, _) => { })
            .ReturnsAsync((IReadOnlyList<Email>)[]);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(configuredConcurrency, capturedConcurrency);
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var emails = new List<Email> { _email };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<Email>>(),
                It.IsAny<Func<Email, SendEmailCommand>>(),
                It.IsAny<Action<Email, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(emails, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_Batch_OnErrorCallback_LogsErrorWithNotificationId()
    {
        // Arrange
        var firstEmail = new Email(Guid.NewGuid(), "Subject 1", "Body 1", "from@test.no", "to1@test.no", EmailContentType.Plain);
        var secondEmail = new Email(Guid.NewGuid(), "Subject 2", "Body 2", "from@test.no", "to2@test.no", EmailContentType.Plain);
        var emails = new List<Email> { firstEmail, secondEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: _ => true);

        var loggerMock = new Mock<ILogger<EmailCommandPublisher>>();
        var publisher = CreatePublisher(messageBusPublisherMock, loggerMock);

        // Act
        await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PublishAsync_Batch_MapsEmailFieldsToCommand()
    {
        // Arrange
        var email = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Plain);
        var emails = new List<Email> { email };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var capturedCommands = new List<SendEmailCommand>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: null, capturedCommands);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        var command = Assert.Single(capturedCommands);
        Assert.Equal(email.Body, command.Body);
        Assert.Equal(email.Subject, command.Subject);
        Assert.Equal(email.ToAddress, command.ToAddress);
        Assert.Equal(email.FromAddress, command.FromAddress);
        Assert.Equal(email.NotificationId, command.NotificationId);
        Assert.Equal(email.ContentType.ToString(), command.ContentType);
    }

    /// <summary>
    /// Configures <paramref name="messageBusPublisherMock"/> so that <c>PublishBatchAsync</c> exercises
    /// the provided <c>commandFactory</c> and <c>onError</c> callbacks the same way the real
    /// <see cref="WolverinePublisher"/> would, without exercising its concurrency internals
    /// (those are covered by the shared <c>WolverinePublisherTests</c>).
    /// </summary>
    /// <param name="messageBusPublisherMock">The mock to configure.</param>
    /// <param name="failurePredicate">Optional predicate selecting which items should be reported as failed.</param>
    /// <param name="capturedCommands">
    /// Optional list that receives every <see cref="SendEmailCommand"/> produced by the real
    /// <c>commandFactory</c>, in item order, so tests can assert field-level mapping.
    /// </param>
    private static void SetupPublishBatch(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Func<Email, bool>? failurePredicate,
        List<SendEmailCommand>? capturedCommands = null)
    {
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<Email>>(),
                It.IsAny<Func<Email, SendEmailCommand>>(),
                It.IsAny<Action<Email, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Email> items, Func<Email, SendEmailCommand> commandFactory, Action<Email, Exception>? onError, CancellationToken _) =>
            {
                var failed = new List<Email>();

                foreach (var item in items)
                {
                    var command = commandFactory(item);
                    capturedCommands?.Add(command);

                    if (failurePredicate is not null && failurePredicate(item))
                    {
                        failed.Add(item);
                        onError?.Invoke(item, new InvalidOperationException("Service Bus unavailable"));
                    }
                }

                return (IReadOnlyList<Email>)failed;
            });
    }

    private static EmailCommandPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Mock<ILogger<EmailCommandPublisher>>? loggerMock = null)
    {
        loggerMock ??= new Mock<ILogger<EmailCommandPublisher>>();

        return new EmailCommandPublisher(loggerMock.Object, messageBusPublisherMock.Object);
    }
}
