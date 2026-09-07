using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.Integrations.Wolverine.Publishers;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

/// <summary>
/// Unit tests for <see cref="PastDueOrderPublisher"/>.
/// </summary>
public class PastDueOrderPublisherTests
{
    private static NotificationOrder CreateOrder() => new() { Id = Guid.NewGuid() };

    [Fact]
    public async Task PublishAsync_EmptyList_DelegatesToMessageBusPublisher()
    {
        // Arrange
        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, exceptionSelector: null);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync([], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusPublisherMock.Verify(
            m => m.PublishBatchAsync(
                It.Is<IReadOnlyList<NotificationOrder>>(items => items.Count == 0),
                It.IsAny<Func<NotificationOrder, ProcessPastDueOrderCommand>>(),
                It.IsAny<Action<NotificationOrder, Exception>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var order = CreateOrder();

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<NotificationOrder>>(),
                It.IsAny<Func<NotificationOrder, ProcessPastDueOrderCommand>>(),
                It.IsAny<Action<NotificationOrder, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync([order], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        var orders = new List<NotificationOrder> { CreateOrder(), CreateOrder() };

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, exceptionSelector: null);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync(orders, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task PublishAsync_AllFail_ReturnsAllOrders()
    {
        // Arrange
        var order1 = CreateOrder();
        var order2 = CreateOrder();

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, exceptionSelector: _ => new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync([order1, order2], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == order1.Id);
        Assert.Contains(result, r => r.Id == order2.Id);
    }

    [Fact]
    public async Task PublishAsync_SomeFail_ReturnsOnlyFailedOrders()
    {
        // Arrange
        var successOrder = CreateOrder();
        var failOrder = CreateOrder();

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(
            messageBusPublisherMock,
            exceptionSelector: order => order.Id == failOrder.Id ? new InvalidOperationException("Service Bus unavailable") : null);

        var publisher = CreatePublisher(messageBusPublisherMock);

        // Act
        var result = await publisher.PublishAsync([successOrder, failOrder], TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(failOrder.Id, result[0].Id);
    }

    [Fact]
    public async Task PublishAsync_OnError_OperationCanceledException_LogsInformationNotError()
    {
        // Arrange
        var order = CreateOrder();

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, exceptionSelector: _ => new OperationCanceledException());

        var loggerMock = new Mock<ILogger<PastDueOrderPublisher>>();
        var publisher = CreatePublisher(messageBusPublisherMock, loggerMock);

        // Act
        var result = await publisher.PublishAsync([order], TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(order.Id, result[0].Id);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_OnError_OtherException_LogsErrorNotInformation()
    {
        // Arrange
        var order = CreateOrder();

        var messageBusPublisherMock = new Mock<IMessageBusPublisher>();
        SetupPublishBatch(messageBusPublisherMock, exceptionSelector: _ => new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<PastDueOrderPublisher>>();
        var publisher = CreatePublisher(messageBusPublisherMock, loggerMock);

        // Act
        var result = await publisher.PublishAsync([order], TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(order.Id, result[0].Id);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Configures <paramref name="messageBusPublisherMock"/> so that <c>PublishBatchAsync</c> exercises
    /// the provided <c>commandFactory</c> and <c>onError</c> callbacks the same way the real
    /// <see cref="WolverinePublisher"/> would, without exercising its concurrency internals
    /// (those are covered by the shared <c>WolverinePublisherTests</c>).
    /// </summary>
    private static void SetupPublishBatch(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Func<NotificationOrder, Exception?>? exceptionSelector)
    {
        messageBusPublisherMock
            .Setup(m => m.PublishBatchAsync(
                It.IsAny<IReadOnlyList<NotificationOrder>>(),
                It.IsAny<Func<NotificationOrder, ProcessPastDueOrderCommand>>(),
                It.IsAny<Action<NotificationOrder, Exception>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<NotificationOrder> items, Func<NotificationOrder, ProcessPastDueOrderCommand> commandFactory, Action<NotificationOrder, Exception>? onError, CancellationToken _) =>
            {
                var failed = new List<NotificationOrder>();

                foreach (var item in items)
                {
                    commandFactory(item);

                    var exception = exceptionSelector?.Invoke(item);
                    if (exception is not null)
                    {
                        failed.Add(item);
                        onError?.Invoke(item, exception);
                    }
                }

                return (IReadOnlyList<NotificationOrder>)failed;
            });
    }

    private static PastDueOrderPublisher CreatePublisher(
        Mock<IMessageBusPublisher> messageBusPublisherMock,
        Mock<ILogger<PastDueOrderPublisher>>? loggerMock = null)
    {
        loggerMock ??= new Mock<ILogger<PastDueOrderPublisher>>();

        return new PastDueOrderPublisher(loggerMock.Object, messageBusPublisherMock.Object);
    }
}
