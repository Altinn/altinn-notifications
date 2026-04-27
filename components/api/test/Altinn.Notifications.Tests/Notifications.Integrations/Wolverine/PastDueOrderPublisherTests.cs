using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Wolverine.Commands;
using Altinn.Notifications.Integrations.Wolverine.Publishers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        var result = await publisher.PublishAsync([], CancellationToken.None);

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
        var result = await publisher.PublishAsync(orders, CancellationToken.None);

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
            () => publisher.PublishAsync([CreateOrder()], CancellationToken.None));
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
        var result = await publisher.PublishAsync([order], CancellationToken.None);

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
        var result = await publisher.PublishAsync([successOrder, failOrder], CancellationToken.None);

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
        var result = await publisher.PublishAsync([order1, order2], CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == order1.Id);
        Assert.Contains(result, r => r.Id == order2.Id);
    }

    private static PastDueOrderPublisher CreatePublisher(
        Mock<IMessageBus> messageBusMock,
        Mock<ILogger<PastDueOrderPublisher>>? loggerMock = null)
    {
        loggerMock ??= new Mock<ILogger<PastDueOrderPublisher>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => messageBusMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        return new PastDueOrderPublisher(loggerMock.Object, serviceProvider);
    }
}
