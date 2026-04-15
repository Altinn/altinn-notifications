using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Kafka.Publishers;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Kafka.Publishers;

public class KafkaSendSmsPublisherTests
{
    private const string _topicName = "altinn.notifications.sms.send";

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

        var publisher = new KafkaSendSmsPublisher(producerMock.Object, _topicName);

        // Act
        var result = await publisher.PublishAsync(sms, CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Equal(_topicName, capturedTopic);
        Assert.Contains(sms.NotificationId.ToString(), capturedPayload);
    }
}
