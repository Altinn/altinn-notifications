using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Publishers;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Kafka.Publishers;

public class KafkaPastDueOrderPublisherTests
{
    private const string _topicName = "altinn.notifications.orders.pastdue";

    private static NotificationOrder CreateOrder() => new() { Id = Guid.NewGuid() };

    private static KafkaPastDueOrderPublisher CreatePublisher(IKafkaProducer producer) =>
        new(producer, Options.Create(new KafkaSettings { PastDueOrdersTopicName = _topicName }));

    [Fact]
    public async Task PublishAsync_EmptyList_ReturnsEmptyListWithoutCallingProducer()
    {
        // Arrange
        var producerMock = new Mock<IKafkaProducer>();
        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync([], CancellationToken.None);

        // Assert
        Assert.Empty(result);
        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        var orders = new List<NotificationOrder> { CreateOrder(), CreateOrder() };

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(_topicName, It.Is<ImmutableList<string>>(msgs => msgs.Count == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList<string>.Empty);

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(orders, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        producerMock.Verify(
            p => p.ProduceAsync(_topicName, It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_SomeFail_ReturnsMatchingOriginalOrders()
    {
        // Arrange
        var order1 = CreateOrder();
        var order2 = CreateOrder();
        var orders = new List<NotificationOrder> { order1, order2 };

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(order2.Serialize()));

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(orders, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(order2.Id, result[0].Id);
    }

    [Fact]
    public async Task PublishAsync_AllFail_ReturnsAllOrders()
    {
        // Arrange
        var order1 = CreateOrder();
        var order2 = CreateOrder();
        var orders = new List<NotificationOrder> { order1, order2 };

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. orders.Select(o => o.Serialize())]);

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(orders, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == order1.Id);
        Assert.Contains(result, r => r.Id == order2.Id);
    }

    [Fact]
    public async Task PublishAsync_ProducerReturnsInvalidJson_IgnoresInvalidEntries()
    {
        // Arrange — "{}" deserializes to Id = Guid.Empty and must be filtered out
        var order = CreateOrder();
        var orders = new List<NotificationOrder> { order };

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(order.Serialize(), "{}"));

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(orders, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(order.Id, result[0].Id);
    }
}
