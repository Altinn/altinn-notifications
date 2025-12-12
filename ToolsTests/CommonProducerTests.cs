using Altinn.Notifications.Integrations.Configuration;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tools;

namespace ToolsTests;

public class CommonProducerTests
{
    [Fact]
    public async Task ProduceAsync_ReturnsTrue_WhenProducerPersists()
    {
        // Arrange
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

        // Act
        var ok = await cp.ProduceAsync("topic", "msg");

        // Assert
        Assert.True(ok);
        mockProducer.Verify(p => p.ProduceAsync(
            "topic",
            It.Is<Message<Null, string>>(m => m.Value == "msg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_ReturnsFalse_WhenStatusIsNotPersisted()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var deliveryResult = new DeliveryResult<Null, string>
        {
            Status = PersistenceStatus.NotPersisted,
            TopicPartitionOffset = new TopicPartitionOffset("t", 0, 0)
        };

        mockProducer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<Null, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deliveryResult);

        var mockLogger = new Mock<ILogger<CommonProducer>>();
        var cp = new CommonProducer(kafkaSettings, mockLogger.Object, mockProducer.Object, shared);

        // Act
        var ok = await cp.ProduceAsync("topic", "msg");

        // Assert
        Assert.False(ok);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message not ack'd by all brokers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_ReturnsFalse_WhenProducerThrowsProduceException()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();

        mockProducer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<Null, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<Null, string>(new Error(ErrorCode.Local_MsgTimedOut), null));

        var mockLogger = new Mock<ILogger<CommonProducer>>();
        var cp = new CommonProducer(kafkaSettings, mockLogger.Object, mockProducer.Object, shared);

        // Act
        var ok = await cp.ProduceAsync("topic", "msg");

        // Assert
        Assert.False(ok);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Permanent error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void EnsureTopicsExist_DoesNotCreate_WhenAllTopicsExist()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        kafkaSettings.Admin.TopicList = new List<string> { "t1", "t2" };
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        var existing = new[] { "t1", "t2" };
        var created = new List<TopicSpecification>();

        // Act
        cp.EnsureTopicsExist(() => existing, spec => created.Add(spec));

        // Assert
        Assert.Empty(created);
    }

    [Fact]
    public void EnsureTopicsExist_CreatesMissingTopics_WithCorrectSpecification()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        kafkaSettings.Admin.TopicList = new List<string> { "t1", "t2", "t3" };
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        var existing = new[] { "t1" };
        var created = new List<TopicSpecification>();

        // Act
        cp.EnsureTopicsExist(() => existing, spec => created.Add(spec));

        // Assert
        Assert.Equal(2, created.Count);
        var names = created.Select(c => c.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "t2", "t3" }, names);

        // verify that created topic specs use values from SharedClientConfig.TopicSpecification
        foreach (var spec in created)
        {
            Assert.Equal(shared.TopicSpecification.NumPartitions, spec.NumPartitions);
            Assert.Equal(shared.TopicSpecification.ReplicationFactor, spec.ReplicationFactor);
            Assert.Equal(shared.TopicSpecification.Configs, spec.Configs);
        }
    }

    [Fact]
    public void EnsureTopicsExist_IsCaseInsensitive_WhenComparingTopics()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        kafkaSettings.Admin.TopicList = new List<string> { "Topic1", "Topic2" };
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        var existing = new[] { "topic1", "TOPIC2" }; // Different casing
        var created = new List<TopicSpecification>();

        // Act
        cp.EnsureTopicsExist(() => existing, spec => created.Add(spec));

        // Assert
        Assert.Empty(created); // Should not create any topics due to case-insensitive comparison
    }

    [Fact]
    public void EnsureTopicsExist_HandlesNullExistingTopics()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        kafkaSettings.Admin.TopicList = new List<string> { "t1" };
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        var created = new List<TopicSpecification>();

        // Act
        cp.EnsureTopicsExist(() => null!, spec => created.Add(spec));

        // Assert
        Assert.Single(created);
        Assert.Equal("t1", created[0].Name);
    }

    [Fact]
    public void EnsureTopicsExist_LogsTopicCreation()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        kafkaSettings.Admin.TopicList = new List<string> { "new-topic" };
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var mockLogger = new Mock<ILogger<CommonProducer>>();
        var cp = new CommonProducer(kafkaSettings, mockLogger.Object, mockProducer.Object, shared);

        var existing = Array.Empty<string>();
        var created = new List<TopicSpecification>();

        // Act
        cp.EnsureTopicsExist(() => existing, spec => created.Add(spec));

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Topic 'new-topic' created successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void EnsureTopicsExist_HandlesEmptyTopicList()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        kafkaSettings.Admin.TopicList = new List<string>();
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        var existing = new[] { "existing-topic" };
        var created = new List<TopicSpecification>();

        // Act
        cp.EnsureTopicsExist(() => existing, spec => created.Add(spec));

        // Assert
        Assert.Empty(created);
    }   

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenProducerIsNull()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        var shared = new SharedClientConfig(kafkaSettings);
        var logger = new NullLogger<CommonProducer>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CommonProducer(kafkaSettings, logger, null!, shared));
    }

    [Fact]
    public void Constructor_CreatesSharedClientConfig_WhenSharedClientConfigIsNull()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        var mockProducer = new Mock<IProducer<Null, string>>();
        var logger = new NullLogger<CommonProducer>();

        // Act
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, null!);

        // Assert - Should not throw and object should be created
        Assert.NotNull(cp);
    }

    [Fact]
    public void Dispose_CallsFlushAndDisposeOnProducer()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        var shared = new SharedClientConfig(kafkaSettings);

        var mockProducer = new Mock<IProducer<Null, string>>();
        mockProducer.Setup(p => p.Flush(It.IsAny<CancellationToken>()));
        mockProducer.Setup(p => p.Dispose());

        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, shared);

        // Act
        cp.Dispose();

        // Assert
        mockProducer.Verify(p => p.Flush(It.IsAny<CancellationToken>()), Times.Once);
        mockProducer.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_HandlesNullProducer()
    {
        // Arrange
        var kafkaSettings = new KafkaSettings();
        var mockProducer = new Mock<IProducer<Null, string>>();
        var logger = new NullLogger<CommonProducer>();
        var cp = new CommonProducer(kafkaSettings, logger, mockProducer.Object, null);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => cp.Dispose());
        Assert.Null(exception);
    }
}
