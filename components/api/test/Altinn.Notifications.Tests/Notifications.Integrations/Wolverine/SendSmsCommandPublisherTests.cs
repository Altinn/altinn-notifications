using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
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
/// Unit tests for <see cref="SendSmsCommandPublisher"/>.
/// </summary>
public class SendSmsCommandPublisherTests
{
    private readonly Sms _sms = new(Guid.NewGuid(), "Altinn", "+4799999999", "Test message body");

    [Fact]
    public async Task PublishAsync_SuccessfulPublish_ReturnsNull()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(_sms, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_ReturnsFailedSms()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(_sms, CancellationToken.None);

        // Assert
        Assert.Equal(_sms, result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_LogsError()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<SendSmsCommandPublisher>>();
        var publisher = CreatePublisher(messageBusMock, loggerMock);

        // Act
        await publisher.PublishAsync(_sms, CancellationToken.None);

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
            () => publisher.PublishAsync(_sms, cts.Token));

        messageBusMock.Verify(
            m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_sms, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_ValidSms_MapsAllFieldsCorrectlyToSendSmsCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var sms = new Sms(notificationId, "TestSender", "+4791234567", "Hello World");

        SendSmsCommand? capturedCommand = null;
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .Callback<SendSmsCommand, DeliveryOptions?>((cmd, _) => capturedCommand = cmd)
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        await publisher.PublishAsync(sms, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("+4791234567", capturedCommand.MobileNumber);
        Assert.Equal("Hello World", capturedCommand.Body);
        Assert.Equal("TestSender", capturedCommand.SenderNumber);
        Assert.Equal(notificationId, capturedCommand.NotificationId);
    }

    [Fact]
    public async Task PublishAsync_Batch_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        var smsList = new List<Sms>
        {
            new(Guid.NewGuid(), "Altinn", "+4791000001", "Msg 1"),
            new(Guid.NewGuid(), "Altinn", "+4791000002", "Msg 2")
        };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(smsList, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PublishAsync_Batch_AllFail_ReturnsAllFailedSms()
    {
        // Arrange
        var sms1 = new Sms(Guid.NewGuid(), "Altinn", "+4791000001", "Msg 1");
        var sms2 = new Sms(Guid.NewGuid(), "Altinn", "+4791000002", "Msg 2");
        var smsList = new List<Sms> { sms1, sms2 };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(smsList, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(sms1, result);
        Assert.Contains(sms2, result);
    }

    [Fact]
    public async Task PublishAsync_Batch_SomeFail_ReturnsOnlyFailedSms()
    {
        // Arrange
        var successSms = new Sms(Guid.NewGuid(), "Altinn", "+4791000001", "Msg");
        var failSms = new Sms(Guid.NewGuid(), "Altinn", "+4791000002", "Msg");
        var smsList = new List<Sms> { successSms, failSms };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendSmsCommand>(c => c.NotificationId == successSms.NotificationId), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendSmsCommand>(c => c.NotificationId == failSms.NotificationId), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(smsList, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Contains(failSms, result);
        Assert.DoesNotContain(successSms, result);
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
        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Batch_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var smsList = new List<Sms> { _sms };
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(smsList, cts.Token));
    }

    [Fact]
    public async Task PublishAsync_Batch_TokenCancelledMidBatch_ThrowsOperationCanceledException()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Msg");
        var smsList = Enumerable.Repeat(sms, 500).ToList();

        using var cts = new CancellationTokenSource();

        var firstSmsStarted = new TaskCompletionSource();
        var firstSmsCanProceed = new TaskCompletionSource();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.Is<SendSmsCommand>(c => c.NotificationId == sms.NotificationId), It.IsAny<DeliveryOptions?>()))
            .Returns<SendSmsCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                firstSmsStarted.TrySetResult();
                await firstSmsCanProceed.Task;
            })));

        var publisher = CreatePublisher(messageBusMock, publishConcurrency: 1);

        // Act
        var publishTask = publisher.PublishAsync(smsList, cts.Token);

        await firstSmsStarted.Task;
        await cts.CancelAsync();
        firstSmsCanProceed.SetResult();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => publishTask);

        messageBusMock.Verify(
            m => m.SendAsync(It.Is<SendSmsCommand>(c => c.NotificationId == sms.NotificationId), It.IsAny<DeliveryOptions?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishAsync_Batch_RespectsSmsPublishConcurrency()
    {
        // Arrange
        const int concurrency = 3;
        const int smsCount = 12;

        var lockObj = new object();
        int currentConcurrent = 0;
        int maxObservedConcurrent = 0;

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns<SendSmsCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                int current = Interlocked.Increment(ref currentConcurrent);
                lock (lockObj)
                {
                    maxObservedConcurrent = Math.Max(maxObservedConcurrent, current);
                }

                await Task.Delay(30);
                Interlocked.Decrement(ref currentConcurrent);
            })));

        var smsList = Enumerable.Range(0, smsCount)
            .Select(_ => new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Msg"))
            .ToList();

        var publisher = CreatePublisher(messageBusMock, publishConcurrency: concurrency);

        // Act
        await publisher.PublishAsync(smsList, CancellationToken.None);

        // Assert
        Assert.True(maxObservedConcurrent > 1, $"Expected concurrent sends but all {smsCount} SMS were processed sequentially.");
        Assert.True(maxObservedConcurrent <= concurrency, $"Max concurrent sends ({maxObservedConcurrent}) exceeded the configured limit ({concurrency}).");

        messageBusMock.Verify(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()), Times.Exactly(smsCount));
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var smsList = new List<Sms> { _sms };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(smsList, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsException_LogsErrorPerFailure()
    {
        // Arrange
        var sms1 = new Sms(Guid.NewGuid(), "Altinn", "+4791000001", "Msg 1");
        var sms2 = new Sms(Guid.NewGuid(), "Altinn", "+4791000002", "Msg 2");
        var smsList = new List<Sms> { sms1, sms2 };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<SendSmsCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<SendSmsCommandPublisher>>();
        var publisher = CreatePublisher(messageBusMock, loggerMock);

        // Act
        await publisher.PublishAsync(smsList, CancellationToken.None);

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

    private static SendSmsCommandPublisher CreatePublisher(
        Mock<IMessageBus> messageBusMock,
        Mock<ILogger<SendSmsCommandPublisher>>? loggerMock = null,
        int publishConcurrency = 10)
    {
        loggerMock ??= new Mock<ILogger<SendSmsCommandPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new WolverineSettings { SmsPublishConcurrency = publishConcurrency });

        return new SendSmsCommandPublisher(loggerMock.Object, serviceProvider, options);
    }
}
