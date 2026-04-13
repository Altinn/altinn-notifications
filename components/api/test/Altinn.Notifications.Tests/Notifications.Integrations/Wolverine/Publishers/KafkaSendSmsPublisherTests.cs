using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Wolverine.Publishers;

using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine.Publishers;

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

        var kafkaSettings = Options.Create(new KafkaSettings { SmsQueueTopicName = _topicName });
        var publisher = new KafkaSendSmsPublisher(producerMock.Object, kafkaSettings);

        // Act
        var result = await publisher.PublishAsync(sms, CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Equal(_topicName, capturedTopic);
        Assert.Contains(sms.NotificationId.ToString(), capturedPayload);
    }
}
