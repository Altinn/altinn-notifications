using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Publishers;

using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Kafka.Publishers;

public class KafkaSendSmsPublisherTests
{
    private const string _topicName = "altinn.notifications.sms.send";

    private static KafkaSendSmsPublisher CreatePublisher(IKafkaProducer producer) =>
        new(producer, Options.Create(new KafkaSettings { SmsQueueTopicName = _topicName }));

    [Fact]
    public async Task PublishAsync_ValidResult_ProducesToCorrectTopic()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Test message");

        string capturedTopic = string.Empty;
        string capturedPayload = string.Empty;

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((topic, payload) =>
            {
                capturedTopic = topic;
                capturedPayload = payload;
            })
            .ReturnsAsync(true);

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(sms, CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Equal(_topicName, capturedTopic);
        Assert.Contains(sms.NotificationId.ToString(), capturedPayload);
    }

    [Fact]
    public async Task PublishAsync_SingleSms_WhenProduceFails_ReturnsSms()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Test message");

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(sms, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sms.NotificationId, result.NotificationId);
    }

    [Fact]
    public async Task PublishAsync_SingleSms_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var sms = new Sms(Guid.NewGuid(), "Altinn", "+4799999999", "Test message");

        var producerMock = new Mock<IKafkaProducer>();
        var publisher = CreatePublisher(producerMock.Object);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.PublishAsync(sms, cts.Token));

        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_BatchSms_AllSucceed_ReturnsEmptyList()
    {
        // Arrange
        IReadOnlyList<Sms> smsList =
        [
            new(Guid.NewGuid(), "Altinn", "+4799999991", "Message 1"),
            new(Guid.NewGuid(), "Altinn", "+4799999992", "Message 2")
        ];

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList<string>.Empty);

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(smsList, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task PublishAsync_BatchSms_SomeFail_ReturnsFailedSms()
    {
        // Arrange
        var failedSms = new Sms(Guid.NewGuid(), "Altinn", "+4799999992", "Message 2");
        IReadOnlyList<Sms> smsList =
        [
            new(Guid.NewGuid(), "Altinn", "+4799999991", "Message 1"),
            failedSms
        ];

        var serializedFailed = JsonSerializer.Serialize(failedSms, JsonSerializerOptionsProvider.Options);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([serializedFailed]);

        var publisher = CreatePublisher(producerMock.Object);

        // Act
        var result = await publisher.PublishAsync(smsList, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(failedSms.NotificationId, result[0].NotificationId);
    }
}
