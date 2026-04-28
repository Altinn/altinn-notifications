using Altinn.Notifications.Sms.Core.Configuration;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations.Publishers;

/// <summary>
/// Unit tests for <see cref="KafkaSmsDeliveryReportPublisher"/>.
/// </summary>
public class KafkaSmsDeliveryReportPublisherTests
{
    [Fact]
    public async Task PublishAsync_ValidResult_ProducesToCorrectTopic()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = "gw-ref-123",
            SendResult = SmsSendResult.Accepted
        };

        string capturedTopic = string.Empty;
        string capturedPayload = string.Empty;

        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((topic, payload) =>
            {
                capturedTopic = topic;
                capturedPayload = payload;
            })
            .ReturnsAsync(true);

        var publisher = new KafkaSmsDeliveryReportPublisher(producerMock.Object, new TopicSettings { SmsStatusUpdatedTopicName = "sms.status.updated" });

        // Act
        await publisher.PublishAsync(result);

        // Assert
        Assert.Equal("sms.status.updated", capturedTopic);
        Assert.Equal(result.Serialize(), capturedPayload);
        producerMock.Verify(p => p.ProduceAsync("sms.status.updated", It.IsAny<string>()), Times.Once);
    }
}
