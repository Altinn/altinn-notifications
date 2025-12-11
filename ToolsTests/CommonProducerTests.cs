using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Integrations.Configuration;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tools;
using Xunit;

namespace ToolsTests;

public class CommonProducerTests
{
    [Fact]
    public async Task ProduceAsync_ReturnsTrue_WhenProducerPersists()
    {
        var kafkaSettings = new KafkaSettings();
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var deliveryResult = new DeliveryResult<Null, string>
        {
            Status = PersistenceStatus.Persisted,
            TopicPartitionOffset = new TopicPartitionOffset("t", 0, 0)
        };

        mockProducer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<Null, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deliveryResult);

        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        var ok = await cp.ProduceAsync("topic", "msg");
        Assert.True(ok);
    }

    [Fact]
    public async Task ProduceAsync_ReturnsFalse_WhenProducerThrowsProduceException()
    {
        var kafkaSettings = new KafkaSettings();
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();

        mockProducer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<Null, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<Null, string>(new Error(ErrorCode.Local_MsgTimedOut), null));

        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        var ok = await cp.ProduceAsync("topic", "msg");
        Assert.False(ok);
    }
}
