using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Producers;

using Moq;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations;

public class SmsSendResultProducerTests
{
    private const string _topicName = "altinn.notifications.sms.send.result";

    [Fact]
    public async Task DispatchAsync_ProducesExactlyOnce()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = "gateway-ref-123",
            SendResult = SmsSendResult.Accepted
        };

        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = new SmsSendResultProducer(producerMock.Object, new KafkaSettings { SmsStatusUpdatedTopicName = _topicName });

        // Act
        await sut.DispatchAsync(result);

        // Assert
        producerMock.Verify(p => p.ProduceAsync(_topicName, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_SerializesResultCorrectly()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        const string gatewayReference = "link-gateway-ref-abc";
        const SmsSendResult sendResult = SmsSendResult.Accepted;

        var result = new SendOperationResult
        {
            NotificationId = notificationId,
            GatewayReference = gatewayReference,
            SendResult = sendResult
        };

        string? capturedMessage = null;
        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, msg) => capturedMessage = msg)
            .ReturnsAsync(true);

        var sut = new SmsSendResultProducer(producerMock.Object, new KafkaSettings { SmsStatusUpdatedTopicName = _topicName });

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains(notificationId.ToString(), capturedMessage);
        Assert.Contains(gatewayReference, capturedMessage);
        Assert.Contains("Accepted", capturedMessage);
    }

    [Fact]
    public async Task DispatchAsync_WhenResultIsNull_ThrowsArgumentNullException()
    {
        var sut = new SmsSendResultProducer(new Mock<ICommonProducer>().Object, new KafkaSettings { SmsStatusUpdatedTopicName = _topicName });
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DispatchAsync(null!));
    }

    [Fact]
    public async Task DispatchAsync_WhenProduceReturnsFalse_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = "gateway-ref",
            SendResult = SmsSendResult.Accepted
        };

        var producerMock = new Mock<ICommonProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var sut = new SmsSendResultProducer(producerMock.Object, new KafkaSettings { SmsStatusUpdatedTopicName = _topicName });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DispatchAsync(result));
    }

    [Fact]
    public async Task DispatchAsync_WhenGatewayReferenceIsEmpty_SerializedMessageIncludesEmptyGatewayReference()
    {
        // Arrange
        var result = new SendOperationResult
        {
            NotificationId = Guid.NewGuid(),
            GatewayReference = string.Empty,
            SendResult = SmsSendResult.Failed
        };

        string? capturedMessage = null;
        var producerMock = new Mock<ICommonProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, msg) => capturedMessage = msg)
            .ReturnsAsync(true);

        var sut = new SmsSendResultProducer(producerMock.Object, new KafkaSettings { SmsStatusUpdatedTopicName = _topicName });

        // Act
        await sut.DispatchAsync(result);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("\"gatewayReference\":\"\"", capturedMessage);
    }
}
