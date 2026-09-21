using System.Collections.Concurrent;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

/// <summary>
/// Unit tests for <see cref="ComposedEmailCommandPublisher"/>.
/// </summary>
public class ComposedEmailCommandPublisherTests
{
    private static readonly Uri _sasUrl = new("https://storage.example.com/container/file.pdf?sv=2021&sig=abc");

    private readonly ComposedEmail _composedEmail = new(
        Guid.NewGuid(),
        "Test Subject",
        "Test Body",
        "sender@altinnxyz.no",
        "recipient@altinnxyz.no",
        EmailContentType.Html,
        [new SasFileReference { Filename = "file.pdf", MimeType = "application/pdf", SasUrl = _sasUrl }]);

    [Fact]
    public async Task PublishAsync_Batch_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        var plainEmail = new ComposedEmail(Guid.NewGuid(), "Plain Subject", "Plain Body", "from@test.no", "plain@test.no", EmailContentType.Plain, []);
        var htmlEmail = new ComposedEmail(Guid.NewGuid(), "Html Subject", "<p>Html Body</p>", "from@test.no", "html@test.no", EmailContentType.Html, []);
        var emails = new List<ComposedEmail> { plainEmail, htmlEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (_, _) => Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusPublisherMock.Verify(
            m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<ComposedEmail>>(),
                It.IsAny<Func<ComposedEmail, SendComposedEmailCommand>>(),
                It.IsAny<Action<ComposedEmail, Exception>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Batch_AllFail_ReturnsAllFailedEmails()
    {
        // Arrange
        var plainEmail = new ComposedEmail(Guid.NewGuid(), "Plain Subject", "Plain Body", "from@test.no", "plain@test.no", EmailContentType.Plain, []);
        var htmlEmail = new ComposedEmail(Guid.NewGuid(), "Html Subject", "<p>Html Body</p>", "from@test.no", "html@test.no", EmailContentType.Html, []);
        var emails = new List<ComposedEmail> { plainEmail, htmlEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (_, _) => throw new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(plainEmail, result);
        Assert.Contains(htmlEmail, result);
    }

    [Fact]
    public async Task PublishAsync_Batch_SomeFail_ReturnsOnlyFailedEmails()
    {
        // Arrange
        var successEmail = new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "from@test.no", "success@test.no", EmailContentType.Plain, []);
        var failEmail = new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "from@test.no", "fail@test.no", EmailContentType.Plain, []);
        var emails = new List<ComposedEmail> { successEmail, failEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (command, _) =>
        {
            if (command.NotificationId == failEmail.NotificationId)
            {
                throw new InvalidOperationException("Service Bus unavailable");
            }

            return Task.CompletedTask;
        });

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Contains(failEmail, result);
        Assert.DoesNotContain(successEmail, result);
    }

    [Fact]
    public async Task PublishAsync_Batch_EmptyList_ReturnsEmptyListWithoutCallingMessageBus()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync([], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusPublisherMock.Verify(
            m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<ComposedEmail>>(),
                It.IsAny<Func<ComposedEmail, SendComposedEmailCommand>>(),
                It.IsAny<Action<ComposedEmail, Exception>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Batch_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var emails = new List<ComposedEmail> { _composedEmail };
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var publisher = CreatePublisher(messageBusPublisherMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(emails, cts.Token));
    }

    [Fact]
    public async Task PublishAsync_Batch_SendFails_ReturnsUnpublishedEmail()
    {
        // Arrange
        var emails = new List<ComposedEmail> { _composedEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (_, _) => throw new Exception("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Contains(_composedEmail, result);
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsException_LogsErrorPerFailure()
    {
        // Arrange
        var plainEmail = new ComposedEmail(Guid.NewGuid(), "Plain Subject", "Plain Body", "from@test.no", "plain@test.no", EmailContentType.Plain, []);
        var htmlEmail = new ComposedEmail(Guid.NewGuid(), "Html Subject", "<p>Html Body</p>", "from@test.no", "html@test.no", EmailContentType.Html, []);
        var emails = new List<ComposedEmail> { plainEmail, htmlEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (_, _) => throw new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<ComposedEmailCommandPublisher>>();
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
    public async Task PublishAsync_Batch_MessageBusCancellation_IsReturnedAsUnpublished()
    {
        // Arrange
        var emails = new List<ComposedEmail> { _composedEmail };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (_, _) => throw new OperationCanceledException());

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Contains(_composedEmail, result);
    }

    [Fact]
    public async Task PublishAsync_ValidComposedEmail_MapsAllBaseFieldsCorrectlyToCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var email = new ComposedEmail(notificationId, "Hello", "<p>World</p>", "from@test.no", "to@test.no", EmailContentType.Html, []);

        SendComposedEmailCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (command, _) =>
        {
            capturedCommand = command;
            return Task.CompletedTask;
        });

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync([email], TestContext.Current.CancellationToken);

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
    public async Task PublishAsync_ValidComposedEmail_MapsAttachmentsCorrectlyToCommand()
    {
        // Arrange
        var sasUri = new Uri("https://storage.example.com/container/report.pdf?sv=2021&sig=xyz");
        var attachment = new SasFileReference { Filename = "report.pdf", MimeType = "application/pdf", SasUrl = sasUri };
        var email = new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Plain, [attachment]);

        SendComposedEmailCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, (command, _) =>
        {
            capturedCommand = command;
            return Task.CompletedTask;
        });

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync([email], TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Single(capturedCommand.Attachments);

        var dto = capturedCommand.Attachments[0];
        Assert.Equal("report.pdf", dto.Filename);
        Assert.Equal("application/pdf", dto.MimeType);
        Assert.Equal(sasUri.ToString(), dto.SasUrl);
    }

    /// <summary>
    /// Configures the <see cref="IMessageBusPublisher.PublishBatchAsync{TItem, TCommand}"/> setup with a simple
    /// unbounded fan-out over all items, delegating the actual "send" behavior to <paramref name="sendAsync"/>
    /// so individual tests can control success or failure per invocation. Concurrency limiting is an
    /// implementation detail of the real <c>WolverinePublisher</c> and is covered by its own tests, so it is
    /// intentionally not simulated here.
    /// </summary>
    private static void SetupPublishBatch(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Func<SendComposedEmailCommand, CancellationToken, Task> sendAsync)
    {
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<ComposedEmail>>(),
                It.IsAny<Func<ComposedEmail, SendComposedEmailCommand>>(),
                It.IsAny<Action<ComposedEmail, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<ComposedEmail>, Func<ComposedEmail, SendComposedEmailCommand>, Action<ComposedEmail, Exception>?, CancellationToken>(
                async (items, commandFactory, onError, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var failed = new ConcurrentBag<ComposedEmail>();

                    await Task.WhenAll(items.Select(async item =>
                    {
                        try
                        {
                            var command = commandFactory(item);
                            await sendAsync(command, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            failed.Add(item);
                            onError?.Invoke(item, ex);
                        }
                    }));

                    return (IReadOnlyList<ComposedEmail>)[.. failed];
                });
    }

    private static ComposedEmailCommandPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Mock<ILogger<ComposedEmailCommandPublisher>>? loggerMock = null)
    {
        loggerMock ??= new Mock<ILogger<ComposedEmailCommandPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusPublisherMock.Object);

        return new ComposedEmailCommandPublisher(loggerMock.Object, messageBusPublisherMock.Object);
    }
}
