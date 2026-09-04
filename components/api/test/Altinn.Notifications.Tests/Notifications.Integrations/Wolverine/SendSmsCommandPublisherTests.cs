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
/// Unit tests for <see cref="SendSmsCommandPublisher"/>.
/// </summary>
public class SendSmsCommandPublisherTests
{
    private readonly Sms _sms = new(Guid.NewGuid(), "Altinn", "+4799999999", "Test message body");

    [Fact]
    public async Task PublishAsync_SuccessfulPublish_ReturnsNull()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(_sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_ReturnsFailedSms()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(_sms, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_sms, result);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_LogsError()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<SendSmsCommandPublisher>>();
        var publisher = CreatePublisher(messageBusPublisherMock, loggerMock);

        // Act
        await publisher.PublishAsync(_sms, TestContext.Current.CancellationToken);

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
            () => publisher.PublishAsync(_sms, cts.Token));

        messageBusPublisherMock.Verify(
            m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(_sms, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_ValidSms_MapsAllFieldsCorrectlyToSendSmsCommand()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var sms = new Sms(notificationId, "TestSender", "+4791234567", "Hello World");

        SendSmsCommand? capturedCommand = null;
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishCommandAsync(It.IsAny<SendSmsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<SendSmsCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(sms, TestContext.Current.CancellationToken);

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

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: null);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(smsList, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task PublishAsync_Batch_AllFail_ReturnsAllFailedSms()
    {
        // Arrange
        var sms1 = new Sms(Guid.NewGuid(), "Altinn", "+4791000001", "Msg 1");
        var sms2 = new Sms(Guid.NewGuid(), "Altinn", "+4791000002", "Msg 2");
        var smsList = new List<Sms> { sms1, sms2 };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: _ => true);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(smsList, TestContext.Current.CancellationToken);

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

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: sms => sms.NotificationId == failSms.NotificationId);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(smsList, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Contains(failSms, result);
        Assert.DoesNotContain(successSms, result);
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
                It.Is<IReadOnlyList<Sms>>(items => items.Count == 0),
                It.IsAny<Func<Sms, SendSmsCommand>>(),
                It.IsAny<Action<Sms, Exception>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Batch_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var smsList = new List<Sms> { _sms };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<Sms>>(),
                It.IsAny<Func<Sms, SendSmsCommand>>(),
                It.IsAny<Action<Sms, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(smsList, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_Batch_OnErrorCallback_LogsErrorWithNotificationId()
    {
        // Arrange
        var sms1 = new Sms(Guid.NewGuid(), "Altinn", "+4791000001", "Msg 1");
        var sms2 = new Sms(Guid.NewGuid(), "Altinn", "+4791000002", "Msg 2");
        var smsList = new List<Sms> { sms1, sms2 };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: _ => true);

        var loggerMock = new Mock<ILogger<SendSmsCommandPublisher>>();
        var publisher = CreatePublisher(messageBusPublisherMock, loggerMock);

        // Act
        await publisher.PublishAsync(smsList, TestContext.Current.CancellationToken);

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
    public async Task PublishAsync_Batch_MapsSmsFieldsToCommand()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Sender", "+4799999999", "Body");
        var smsList = new List<Sms> { sms };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        var capturedCommands = new List<SendSmsCommand>();
        SetupPublishBatch(messageBusPublisherMock, failurePredicate: null, capturedCommands);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        await publisher.PublishAsync(smsList, TestContext.Current.CancellationToken);

        // Assert
        var command = Assert.Single(capturedCommands);
        Assert.Equal(sms.Recipient, command.MobileNumber);
        Assert.Equal(sms.Message, command.Body);
        Assert.Equal(sms.Sender, command.SenderNumber);
        Assert.Equal(sms.NotificationId, command.NotificationId);
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
    /// Optional list that receives every <see cref="SendSmsCommand"/> produced by the real
    /// <c>commandFactory</c>, in item order, so tests can assert field-level mapping.
    /// </param>
    private static void SetupPublishBatch(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Func<Sms, bool>? failurePredicate,
        List<SendSmsCommand>? capturedCommands = null)
    {
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<Sms>>(),
                It.IsAny<Func<Sms, SendSmsCommand>>(),
                It.IsAny<Action<Sms, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Sms> items, Func<Sms, SendSmsCommand> commandFactory, Action<Sms, Exception>? onError, CancellationToken _) =>
            {
                var failed = new List<Sms>();

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

                return (IReadOnlyList<Sms>)failed;
            });
    }

    private static SendSmsCommandPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Mock<ILogger<SendSmsCommandPublisher>>? loggerMock = null)
    {
        loggerMock ??= new Mock<ILogger<SendSmsCommandPublisher>>();

        return new SendSmsCommandPublisher(loggerMock.Object, messageBusPublisherMock.Object);
    }
}
