using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine.Publishers;

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
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(_email, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_ReturnsFailedEmail()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(_email, CancellationToken.None);

        // Assert
        Assert.Equal(_email, result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_LogsError()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<EmailCommandPublisher>>();
        var publisher = CreatePublisher(messageBusMock, loggerMock);

        // Act
        await publisher.PublishAsync(_email, CancellationToken.None);

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
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, cts.Token));

        messageBusMock.Verify(
            m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_email, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_ValidEmail_MapsAllFieldsCorrectlyToSendEmailCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var email = new Email(notificationId, "Hello", "<p>World</p>", "from@test.no", "to@test.no", EmailContentType.Html);

        SendEmailCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishAsync(email, CancellationToken.None);

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
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishAsync(email, CancellationToken.None);

        // Assert
        Assert.Equal("Plain", capturedCommand!.ContentType);
    }

    [Fact]
    public async Task PublishAsync_HtmlContentType_MapsContentTypeEnumToString()
    {
        // Arrange
        var email = new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Html);

        SendEmailCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendEmailCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishAsync(email, CancellationToken.None);

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

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(emails, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PublishAsync_Batch_AllFail_ReturnsAllFailedEmails()
    {
        // Arrange
        var email1 = new Email(Guid.NewGuid(), "Subject 1", "Body 1", "from@test.no", "to1@test.no", EmailContentType.Plain);
        var email2 = new Email(Guid.NewGuid(), "Subject 2", "Body 2", "from@test.no", "to2@test.no", EmailContentType.Plain);
        var emails = new List<Email> { email1, email2 };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(emails, CancellationToken.None);

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

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendEmailCommand>(c => c.NotificationId == successEmail.NotificationId), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendEmailCommand>(c => c.NotificationId == failEmail.NotificationId), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(emails, CancellationToken.None);

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
        var result = await publisher.PublishAsync([], CancellationToken.None);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Batch_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var emails = new List<Email> { _email };
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(emails, cts.Token));
    }

    [Fact]
    public async Task PublishAsync_Batch_TokenCancelledMidBatch_ThrowsOperationCanceledException()
    {
        // Arrange
        var email = new Email(Guid.NewGuid(), "Subject 1", "Body 1", "from@test.no", "to@test.no", EmailContentType.Plain);
        var emails = Enumerable.Repeat(email, 500).ToList();

        using var cts = new CancellationTokenSource();

        var firstEmailStarted = new TaskCompletionSource();
        var firstEmailCanProceed = new TaskCompletionSource();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendEmailCommand>(c => c.NotificationId == email.NotificationId), It.IsAny<DeliveryOptions?>()))
            .Returns<SendEmailCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                firstEmailStarted.SetResult();
                await firstEmailCanProceed.Task;
            })));

        var publisher = CreatePublisher(messageBusMock, publishConcurrency: 1);

        // Act
        var publishTask = publisher.PublishAsync(emails, cts.Token);

        await firstEmailStarted.Task;
        await cts.CancelAsync();
        firstEmailCanProceed.SetResult();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => publishTask);

        messageBusMock.Verify(
            m => m.SendAsync(It.Is<SendEmailCommand>(c => c.NotificationId == email.NotificationId), It.IsAny<DeliveryOptions?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishAsync_Batch_RespectsEmailPublishConcurrency()
    {
        // Arrange
        const int concurrency = 3;
        const int emailCount = 12;

        var lockObj = new object();
        int currentConcurrent = 0;
        int maxObservedConcurrent = 0;

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns<SendEmailCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                int current = Interlocked.Increment(ref currentConcurrent);
                lock (lockObj)
                {
                    maxObservedConcurrent = Math.Max(maxObservedConcurrent, current);
                }
                
                await Task.Delay(30);
                Interlocked.Decrement(ref currentConcurrent);
            })));

        var emails = Enumerable.Range(0, emailCount)
            .Select(_ => new Email(Guid.NewGuid(), "Subject", "Body", "from@test.no", "to@test.no", EmailContentType.Plain))
            .ToList();

        var publisher = CreatePublisher(messageBusMock, publishConcurrency: concurrency);

        // Act
        await publisher.PublishAsync(emails, CancellationToken.None);

        // Assert
        Assert.True(maxObservedConcurrent > 1, $"Expected concurrent sends but all {emailCount} emails were processed sequentially.");
        Assert.True(maxObservedConcurrent <= concurrency, $"Max concurrent sends ({maxObservedConcurrent}) exceeded the configured limit ({concurrency}).");

        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()), Times.Exactly(emailCount));
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var emails = new List<Email> { _email };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(emails, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsException_LogsErrorPerFailure()
    {
        // Arrange
        var firstEmail = new Email(Guid.NewGuid(), "Subject 1", "Body 1", "from@test.no", "to1@test.no", EmailContentType.Plain);
        var secondEmail = new Email(Guid.NewGuid(), "Subject 2", "Body 2", "from@test.no", "to2@test.no", EmailContentType.Plain);
        var emails = new List<Email> { firstEmail, secondEmail };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendEmailCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<EmailCommandPublisher>>();
        var publisher = CreatePublisher(messageBusMock, loggerMock);

        // Act
        await publisher.PublishAsync(emails, CancellationToken.None);

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

    private static EmailCommandPublisher CreatePublisher(
        Mock<IMessageBus> messageBusMock,
        Mock<ILogger<EmailCommandPublisher>>? loggerMock = null,
        int publishConcurrency = 10)
    {
        loggerMock ??= new Mock<ILogger<EmailCommandPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new WolverineSettings { EmailPublishConcurrency = publishConcurrency });

        return new EmailCommandPublisher(loggerMock.Object, serviceProvider, options);
    }
}
