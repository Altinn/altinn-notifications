using System.Collections.Concurrent;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
                It.IsAny<int>(),
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
                It.IsAny<int>(),
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
    public async Task PublishAsync_Batch_TokenCancelledMidBatch_ReturnsUnpublishedEmails()
    {
        // Arrange
        var firstEmail = new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "from@test.no", "first@test.no", EmailContentType.Plain, []);
        var secondEmail = new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "from@test.no", "second@test.no", EmailContentType.Plain, []);
        var emails = new List<ComposedEmail> { firstEmail, secondEmail };

        int invocationCount = 0;
        Guid firstPublishedNotificationId = Guid.Empty;
        var firstEmailStarted = new TaskCompletionSource();
        var firstEmailCanProceed = new TaskCompletionSource();

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, async (command, _) =>
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                firstPublishedNotificationId = command.NotificationId;
                firstEmailStarted.TrySetResult();
                await firstEmailCanProceed.Task;
            }
        });

        var publisher = CreatePublisher(messageBusPublisherMock, publishConcurrency: 1);

        using var cts = new CancellationTokenSource();

        // Act
        var publishTask = publisher.PublishAsync(emails, cts.Token);

        await firstEmailStarted.Task;
        await cts.CancelAsync();
        firstEmailCanProceed.SetResult();

        var result = await publishTask;

        // Assert
        ComposedEmail publishedEmail = emails.Single(e => e.NotificationId == firstPublishedNotificationId);
        ComposedEmail unpublishedEmail = emails.Single(e => e.NotificationId != firstPublishedNotificationId);

        Assert.DoesNotContain(publishedEmail, result);
        Assert.True(result.Count <= 1, $"Expected at most one unpublished email in a two-email batch, got {result.Count}.");

        if (result.Count == 1)
        {
            Assert.Contains(unpublishedEmail, result);
        }
    }

    [Fact]
    public async Task PublishAsync_Batch_RespectsComposedEmailPublishConcurrency()
    {
        // Arrange
        const int concurrency = 5;
        const int emailCount = 500;

        int currentConcurrent = 0;
        int maxObservedConcurrent = 0;

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, async (_, _) =>
        {
            int current = Interlocked.Increment(ref currentConcurrent);

            Interlocked.Exchange(ref maxObservedConcurrent, Math.Max(Volatile.Read(ref maxObservedConcurrent), current));

            await Task.Delay(100, TestContext.Current.CancellationToken);

            Interlocked.Decrement(ref currentConcurrent);
        });

        var emails = Enumerable.Range(0, emailCount)
            .Select(_ => new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Plain, []))
            .ToList();

        var publisher = CreatePublisher(messageBusPublisherMock, publishConcurrency: concurrency);

        // Act
        await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(maxObservedConcurrent > 1, $"Expected concurrent sends but all {emailCount} emails were processed sequentially.");
        Assert.True(maxObservedConcurrent <= concurrency, $"Max concurrent sends ({maxObservedConcurrent}) exceeded the configured limit ({concurrency}).");
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
    /// Configures the <see cref="IMessageBusPublisher.PublishBatchAsync{TItem, TCommand}"/> setup to replicate
    /// the real Wolverine-backed implementation's per-item send, concurrency limiting and failure tracking,
    /// while delegating the actual "send" behavior to <paramref name="sendAsync"/> so individual tests can
    /// control success or failure per invocation.
    /// </summary>
    private static void SetupPublishBatch(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Func<SendComposedEmailCommand, CancellationToken, Task> sendAsync)
    {
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<ComposedEmail>>(),
                It.IsAny<Func<ComposedEmail, SendComposedEmailCommand>>(),
                It.IsAny<int>(),
                It.IsAny<Action<ComposedEmail, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<ComposedEmail>, Func<ComposedEmail, SendComposedEmailCommand>, int, Action<ComposedEmail, Exception>?, CancellationToken>(
                async (items, commandFactory, concurrency, onError, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var failed = new ConcurrentBag<ComposedEmail>();
                    using var semaphore = new SemaphoreSlim(concurrency);

                    await Task.WhenAll(items.Select(async item =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
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
                        finally
                        {
                            semaphore.Release();
                        }
                    }));

                    return (IReadOnlyList<ComposedEmail>)[.. failed];
                });
    }

    private static ComposedEmailCommandPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Mock<ILogger<ComposedEmailCommandPublisher>>? loggerMock = null,
        int publishConcurrency = 5)
    {
        loggerMock ??= new Mock<ILogger<ComposedEmailCommandPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusPublisherMock.Object);

        var options = Options.Create(new WolverineSettings { ComposedEmailPublishConcurrency = publishConcurrency });

        return new ComposedEmailCommandPublisher(loggerMock.Object, messageBusPublisherMock.Object, options);
    }
}
