using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.Integrations.Wolverine.Publishers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Wolverine;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class PastDueOrderPublisherTests
{
    private static NotificationOrder CreateOrder() => new() { Id = Guid.NewGuid() };

    [Fact]
    public async Task PublishAsync_EmptyList_ReturnsEmptyListWithoutCallingMessageBus()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync([], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(
            m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()),
            Times.Never);
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
            () => publisher.PublishAsync([CreateOrder()], cts.Token));

        messageBusMock.Verify(
            m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        var orders = new List<NotificationOrder> { CreateOrder(), CreateOrder() };

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync(orders, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        messageBusMock.Verify(
            m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsOperationCanceledException_Rethrows()
    {
        // Arrange
        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new OperationCanceledException());

        var publisher = CreatePublisher(messageBusMock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync([CreateOrder()], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_MessageBusThrowsException_LogsErrorAndReturnsFailedOrder()
    {
        // Arrange
        var order = CreateOrder();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var loggerMock = new Mock<ILogger<PastDueOrderPublisher>>();
        var publisher = CreatePublisher(messageBusMock, loggerMock);

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
    }

    [Fact]
    public async Task PublishAsync_SomeFail_ReturnsOnlyFailedOrders()
    {
        // Arrange
        var successOrder = CreateOrder();
        var failOrder = CreateOrder();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(
                It.Is<ProcessPastDueOrderCommand>(c => c.Order.Id == successOrder.Id),
                It.IsAny<DeliveryOptions?>()))
            .Returns(ValueTask.CompletedTask);
        messageBusMock
            .Setup(m => m.SendAsync(
                It.Is<ProcessPastDueOrderCommand>(c => c.Order.Id == failOrder.Id),
                It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync([successOrder, failOrder], TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(failOrder.Id, result[0].Id);
    }

    [Fact]
    public async Task PublishAsync_AllFail_ReturnsAllOrders()
    {
        // Arrange
        var order1 = CreateOrder();
        var order2 = CreateOrder();

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var publisher = CreatePublisher(messageBusMock);

        // Act
        var result = await publisher.PublishAsync([order1, order2], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == order1.Id);
        Assert.Contains(result, r => r.Id == order2.Id);
    }

    [Fact]
    public async Task PublishAsync_RespectsConcurrencyLimit()
    {
        // Arrange
        const int concurrency = 3;
        const int orderCount = 12;

        var lockObj = new object();
        int currentConcurrent = 0;
        int maxObservedConcurrent = 0;

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns<ProcessPastDueOrderCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                int current = Interlocked.Increment(ref currentConcurrent);
                lock (lockObj)
                {
                    maxObservedConcurrent = Math.Max(maxObservedConcurrent, current);
                }

                await Task.Delay(30);
                Interlocked.Decrement(ref currentConcurrent);
            })));

        var orders = Enumerable.Range(0, orderCount)
            .Select(_ => CreateOrder())
            .ToList();

        var publisher = CreatePublisher(messageBusMock, concurrency: concurrency);

        // Act
        await publisher.PublishAsync(orders, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(maxObservedConcurrent > 1, $"Expected concurrent sends but all {orderCount} orders were processed sequentially.");
        Assert.True(maxObservedConcurrent <= concurrency, $"Max concurrent sends ({maxObservedConcurrent}) exceeded the configured limit ({concurrency}).");

        messageBusMock.Verify(
        m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()),
        Times.Exactly(orderCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PublishAsync_ZeroOrNegativeConcurrency_DefaultsToTen(int concurrency)
    {
        // Arrange
        const int orderCount = 20;

        var lockObj = new object();
        int currentConcurrent = 0;
        int maxObservedConcurrent = 0;

        var messageBusMock = new Mock<IMessageBus>();
        messageBusMock
            .Setup(m => m.SendAsync(It.IsAny<ProcessPastDueOrderCommand>(), It.IsAny<DeliveryOptions?>()))
            .Returns<ProcessPastDueOrderCommand, DeliveryOptions?>((_, _) => new ValueTask(Task.Run(async () =>
            {
                int current = Interlocked.Increment(ref currentConcurrent);
                lock (lockObj)
                {
                    maxObservedConcurrent = Math.Max(maxObservedConcurrent, current);
                }

                await Task.Delay(30);
                Interlocked.Decrement(ref currentConcurrent);
            })));                                
        
        var publisher = CreatePublisher(messageBusMock, concurrency : concurrency);

        var orders = Enumerable.Range(0, orderCount)
            .Select(_ => CreateOrder())
            .ToList();
        
        // Act
        var result = await publisher.PublishAsync(orders, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.True(maxObservedConcurrent > 1, "Expected concurrent processing but all orders ran sequentially.");
        Assert.True(maxObservedConcurrent <= 10, $"Max concurrent ({maxObservedConcurrent}) exceeded the expected default of 10.");
    }

    private static PastDueOrderPublisher CreatePublisher(
        Mock<IMessageBus> messageBusMock,
        Mock<ILogger<PastDueOrderPublisher>>? loggerMock = null,
        int concurrency = 10)
    {
        loggerMock ??= new Mock<ILogger<PastDueOrderPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new WolverineSettings { PastDueOrdersPublishConcurrency = concurrency });

        return new PastDueOrderPublisher(loggerMock.Object, serviceProvider, options);
    }
}
