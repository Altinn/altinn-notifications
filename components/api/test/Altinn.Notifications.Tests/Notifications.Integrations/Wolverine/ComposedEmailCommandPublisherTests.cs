using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Wolverine;

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

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PublishAsync_Batch_AllFail_ReturnsAllFailedEmails()
    {
        // Arrange
        var plainEmail = new ComposedEmail(Guid.NewGuid(), "Plain Subject", "Plain Body", "from@test.no", "plain@test.no", EmailContentType.Plain, []);
        var htmlEmail = new ComposedEmail(Guid.NewGuid(), "Html Subject", "<p>Html Body</p>", "from@test.no", "html@test.no", EmailContentType.Html, []);
        var emails = new List<ComposedEmail> { plainEmail, htmlEmail };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

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

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendComposedEmailCommand>(c => c.NotificationId == successEmail.NotificationId), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendComposedEmailCommand>(c => c.NotificationId == failEmail.NotificationId), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

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
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync([], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Batch_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var emails = new List<ComposedEmail> { _composedEmail };
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

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

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new Exception("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

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

        using var cts = new CancellationTokenSource();

        int invocationCount = 0;
        Guid firstPublishedNotificationId = Guid.Empty;
        var firstEmailStarted = new TaskCompletionSource();
        var firstEmailCanProceed = new TaskCompletionSource();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns<SendComposedEmailCommand, DeliveryOptions?>((command, _) => new ValueTask(Task.Run(async () =>
            {
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    firstPublishedNotificationId = command.NotificationId;
                    firstEmailStarted.TrySetResult();
                    await firstEmailCanProceed.Task;
                }
            })));

        var publisher = CreatePublisher(messageBusMock, publishConcurrency: 1);

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

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(e => e.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns<SendComposedEmailCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                int current = Interlocked.Increment(ref currentConcurrent);

                Interlocked.Exchange(ref maxObservedConcurrent, Math.Max(Volatile.Read(ref maxObservedConcurrent), current));

                await Task.Delay(100);

                Interlocked.Decrement(ref currentConcurrent);
            })));

        var emails = Enumerable.Range(0, emailCount)
            .Select(_ => new ComposedEmail(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Plain, []))
            .ToList();

        var publisher = CreatePublisher(messageBusMock, publishConcurrency: concurrency);

        // Act
        await publisher.PublishAsync(emails, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(maxObservedConcurrent > 1, $"Expected concurrent sends but all {emailCount} emails were processed sequentially.");
        Assert.True(maxObservedConcurrent <= concurrency, $"Max concurrent sends ({maxObservedConcurrent}) exceeded the configured limit ({concurrency}).");

        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()), Times.Exactly(emailCount));
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsException_LogsErrorPerFailure()
    {
        // Arrange
        var plainEmail = new ComposedEmail(Guid.NewGuid(), "Plain Subject", "Plain Body", "from@test.no", "plain@test.no", EmailContentType.Plain, []);
        var htmlEmail = new ComposedEmail(Guid.NewGuid(), "Html Subject", "<p>Html Body</p>", "from@test.no", "html@test.no", EmailContentType.Html, []);
        var emails = new List<ComposedEmail> { plainEmail, htmlEmail };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<ComposedEmailCommandPublisher>>();
        var publisher = CreatePublisher(messageBusMock, loggerMock);

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

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

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
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendComposedEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

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
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendComposedEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendComposedEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

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

    private static ComposedEmailCommandPublisher CreatePublisher(
        Mock<IMessageBus> messageBusMock,
        Mock<ILogger<ComposedEmailCommandPublisher>>? loggerMock = null,
        int publishConcurrency = 5)
    {
        loggerMock ??= new Mock<ILogger<ComposedEmailCommandPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new WolverineSettings { ComposedEmailPublishConcurrency = publishConcurrency });

        return new ComposedEmailCommandPublisher(loggerMock.Object, serviceProvider, options);
    }
}
