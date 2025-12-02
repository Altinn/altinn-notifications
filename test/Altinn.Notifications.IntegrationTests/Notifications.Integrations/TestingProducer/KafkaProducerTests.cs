using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Reflection;

using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Producers;
using Altinn.Notifications.IntegrationTests.Utils;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Integrations.TestingProducer;

public class KafkaProducerTests : IAsyncLifetime
{
    private KafkaProducer? _sharedProducer;

    private const int _metricEmissionDelayMs = 25;
    private const string _brokerAddress = "localhost:9092";
    private readonly string _batchTopic = $"kafka-producer-batch-{Guid.NewGuid():N}";
    private readonly string _validTopic = $"kafka-producer-valid-{Guid.NewGuid():N}";
    private readonly string _invalidTopic = $"kafka-producer-invalid-{Guid.NewGuid():N}";

    /// <summary>
    /// Disposes the class instance, after all tests have been run.
    /// </summary>
    public async Task DisposeAsync()
    {
        _sharedProducer?.Dispose();

        await KafkaUtil.DeleteTopicAsync(_batchTopic);
        await KafkaUtil.DeleteTopicAsync(_validTopic);
        await KafkaUtil.DeleteTopicAsync(_invalidTopic);
    }

    /// <summary>
    /// Initializes the class instance, creating the necessary Kafka topics.
    /// </summary>
    public async Task InitializeAsync()
    {
        await KafkaUtil.CreateTopicAsync(_validTopic);
        await KafkaUtil.CreateTopicAsync(_batchTopic);

        var settings = CreateKafkaSettings(_validTopic, _batchTopic);
        var nullLogger = Mock.Of<ILogger<KafkaProducer>>();
        _sharedProducer = new KafkaProducer(Options.Create(settings), nullLogger);
    }

    [Fact]
    public async Task ProduceAsync_SingleMessage_WithInvalidTopic_ReturnsFalseAndLogsError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        using var producer = CreateTestProducer(loggerMock.Object);

        // Act
        var result = await producer.ProduceAsync(_invalidTopic, "valid-payload");

        // Assert
        Assert.False(result);
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Topic name is not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_SingleMessage_WithWhitespaceMessage_ReturnsFalseAndLogsError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        using var producer = CreateTestProducer(loggerMock.Object);

        // Act
        var result = await producer.ProduceAsync(_validTopic, "   ");

        // Assert
        Assert.False(result);
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Message is null, empty, or whitespace")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_SingleMessage_WhenPersisted_ReturnsTrueAndIncrementsPublishedCounter()
    {
        // Arrange
        var publishedCounterDelta = 0;
        const string testMessage = "test-message";
        var loggerMock = new Mock<ILogger<KafkaProducer>>();

        using var producer = CreateTestProducer(loggerMock.Object);
        using var listener = CreatePublishedCounterListener(delta => Interlocked.Add(ref publishedCounterDelta, delta));

        var mockProducer = new Mock<IProducer<Null, string>>();
        mockProducer
            .Setup(e => e.ProduceAsync(_validTopic, It.Is<Message<Null, string>>(m => m.Value == testMessage), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<Null, string>
            {
                Status = PersistenceStatus.Persisted,
                Message = new Message<Null, string> { Value = testMessage }
            });

        InjectMockProducer(producer, mockProducer.Object);

        // Act
        var result = await producer.ProduceAsync(_validTopic, testMessage);
        await Task.Delay(_metricEmissionDelayMs);

        // Assert
        Assert.True(result);
        Assert.Equal(1, publishedCounterDelta);
        loggerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProduceAsync_SingleMessage_WhenNotPersisted_ReturnsFalseAndIncrementsFailedCounter()
    {
        // Arrange
        var failedCounterDelta = 0;
        var latencyMeasurements = 0;
        const string testMessage = "test-message";
        var loggerMock = new Mock<ILogger<KafkaProducer>>();

        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));
        using var latencyListener = CreateLatencyListener(_ => Interlocked.Increment(ref latencyMeasurements));

        var mockProducer = new Mock<IProducer<Null, string>>();
        mockProducer
            .Setup(e => e.ProduceAsync(_validTopic, It.Is<Message<Null, string>>(m => m.Value == testMessage), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<Null, string>
            {
                Status = PersistenceStatus.NotPersisted,
                Message = new Message<Null, string> { Value = testMessage }
            });

        InjectMockProducer(producer, mockProducer.Object);

        // Act
        var result = await producer.ProduceAsync(_validTopic, testMessage);
        await Task.Delay(_metricEmissionDelayMs);

        // Assert
        Assert.False(result);
        Assert.Equal(1, failedCounterDelta);
        Assert.True(latencyMeasurements > 0, "Expected at least one latency measurement");

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_SingleMessage_WhenProduceExceptionThrown_ReturnsFalseAndIncrementsFailedCounter()
    {
        // Arrange
        var failedCounterDelta = 0;
        var publishedCounterDelta = 0;
        const string testMessage = "test-message";

        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));
        using var publishedListener = CreatePublishedCounterListener(delta => Interlocked.Add(ref publishedCounterDelta, delta));

        var produceException = new ProduceException<Null, string>(
            new Error(ErrorCode.Local_MsgTimedOut, "Simulated timeout"),
            new DeliveryResult<Null, string> { Status = PersistenceStatus.NotPersisted });

        var mockProducer = new Mock<IProducer<Null, string>>();
        mockProducer
            .Setup(e => e.ProduceAsync(_validTopic, It.IsAny<Message<Null, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(produceException);
        InjectMockProducer(producer, mockProducer.Object);

        // Act
        var result = await producer.ProduceAsync(_validTopic, testMessage);
        var success = SpinWait.SpinUntil(() => Volatile.Read(ref failedCounterDelta) == 1, TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.False(result);
        Assert.Equal(1, failedCounterDelta);
        Assert.Equal(0, publishedCounterDelta);
        Assert.True(success, $"Counter did not reach expected value {1} within timeout");

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                produceException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_SingleMessage_WhenUnexpectedExceptionThrown_ReturnsFalseAndIncrementsFailedCounter()
    {
        // Arrange
        var failedCounterDelta = 0;
        const string testMessage = "test-message";

        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var unexpectedException = new InvalidOperationException("Unexpected error");

        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));

        var mockProducer = new Mock<IProducer<Null, string>>();
        mockProducer
            .Setup(e => e.ProduceAsync(_validTopic, It.IsAny<Message<Null, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(unexpectedException);
        InjectMockProducer(producer, mockProducer.Object);

        // Act
        var result = await producer.ProduceAsync(_validTopic, testMessage);
        await Task.Delay(_metricEmissionDelayMs);

        // Assert
        Assert.False(result);
        Assert.Equal(1, failedCounterDelta);
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                unexpectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_BatchMessages_WithEmptyBatch_ReturnsSameBatchAndLogsError()
    {
        // Arrange
        var failedCounterDelta = 0;
        var emptyBatch = ImmutableList<string>.Empty;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();

        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));

        // Act
        var result = await producer.ProduceAsync(_batchTopic, emptyBatch);
        await Task.Delay(_metricEmissionDelayMs);

        // Assert
        Assert.Same(emptyBatch, result);
        Assert.Equal(0, failedCounterDelta);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("No messages to produce")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProduceAsync_BatchMessages_WithInvalidTopic_ReturnsOriginalBatchAndIncrementsFailedCounter()
    {
        // Arrange
        var failedCounterDelta = 0;
        var publishedCounterDelta = 0;
        var invalidTopic = $"invalid-{Guid.NewGuid():N}";
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var validBatch = ImmutableList.Create("message1", "message2", "message3");

        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));
        using var publishedListener = CreatePublishedCounterListener(delta => Interlocked.Add(ref publishedCounterDelta, delta));

        // Act
        var result = await producer.ProduceAsync(invalidTopic, validBatch);
        var success = SpinWait.SpinUntil(() => Volatile.Read(ref failedCounterDelta) == validBatch.Count, TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.Same(validBatch, result);
        Assert.Equal(0, publishedCounterDelta);
        Assert.Equal(validBatch.Count, failedCounterDelta);
        Assert.True(success, $"Counter did not reach expected value {validBatch.Count} within timeout");

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Topic name is not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProduceAsync_BatchMessages_WithAllInvalidMessages_ReturnsSameBatchAndIncrementsFailedCounter()
    {
        // Arrange
        var failedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var invalidBatch = ImmutableList.Create(null!, string.Empty, "   ");

        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));

        // Act
        var result = await producer.ProduceAsync(_batchTopic, invalidBatch);
        var success = SpinWait.SpinUntil(() => Volatile.Read(ref failedCounterDelta) == invalidBatch.Count, TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.Same(invalidBatch, result);
        Assert.Equal(invalidBatch.Count, failedCounterDelta);
        Assert.True(success, $"Counter did not reach expected value {invalidBatch.Count} within timeout");

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProduceAsync_BatchMessages_WithMixedSuccessAndNotPersisted_ReturnsInvalidAndNotPersistedMessages()
    {
        // Arrange
        var failedCounterDelta = 0;
        var publishedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();

        var invalidMessage = "   ";
        var firstValidMessage = "valid-message-1";
        var secondValidMessage = "valid-message-2";
        var mixedBatch = ImmutableList.Create(firstValidMessage, invalidMessage, secondValidMessage);

        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));
        using var publishedListener = CreatePublishedCounterListener(delta => Interlocked.Add(ref publishedCounterDelta, delta));

        var mockProducer = new Mock<IProducer<Null, string>>();

        // Setup first message to succeed (Persisted)
        mockProducer
            .Setup(e => e.ProduceAsync(_batchTopic, It.Is<Message<Null, string>>(m => m.Value == firstValidMessage), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<Null, string>
            {
                Status = PersistenceStatus.Persisted,
                Message = new Message<Null, string> { Value = firstValidMessage }
            });

        // Setup second message to fail (NotPersisted)
        mockProducer
            .Setup(e => e.ProduceAsync(_batchTopic, It.Is<Message<Null, string>>(m => m.Value == secondValidMessage), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<Null, string>
            {
                Status = PersistenceStatus.NotPersisted,
                Message = new Message<Null, string> { Value = secondValidMessage }
            });

        InjectMockProducer(producer, mockProducer.Object);

        // Act
        var result = await producer.ProduceAsync(_batchTopic, mixedBatch);
        var success = SpinWait.SpinUntil(() => Volatile.Read(ref failedCounterDelta) >= 2, TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(2, failedCounterDelta);
        Assert.Equal(1, publishedCounterDelta);
        Assert.True(success, "Failed counter did not reach expected value within timeout");

        Assert.Contains(invalidMessage, result);
        Assert.Contains(secondValidMessage, result);
        Assert.DoesNotContain(firstValidMessage, result);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_BatchMessages_WhenAllTasksThrowExceptions_ReturnsAllMessagesAndIncrementsFailedCounter()
    {
        // Arrange
        var failedCounterDelta = 0;
        var publishedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var testTopic = $"kafka-producer-throw-test-{Guid.NewGuid():N}";
        var validBatch = ImmutableList.Create("message1", "message2", "message3");

        await KafkaUtil.CreateTopicAsync(testTopic);

        try
        {
            var settings = CreateKafkaSettings(testTopic);
            using var producer = new KafkaProducer(Options.Create(settings), loggerMock.Object);

            // Replace with failing producer
            InjectMockProducer(producer, new FailingProducer());

            using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));
            using var publishedListener = CreatePublishedCounterListener(delta => Interlocked.Add(ref publishedCounterDelta, delta));

            // Act
            var result = await producer.ProduceAsync(testTopic, validBatch);
            var success = SpinWait.SpinUntil(() => Volatile.Read(ref failedCounterDelta) == validBatch.Count, TimeSpan.FromMilliseconds(500));

            // Assert
            Assert.Equal(0, publishedCounterDelta);
            Assert.Equal(validBatch.Count, result.Count);
            Assert.Equal(validBatch.Count, failedCounterDelta);
            Assert.True(validBatch.SequenceEqual(result), "Returned messages should match input batch");
            Assert.True(success, $"Counter did not reach expected value {validBatch.Count} within timeout");

            loggerMock.Verify(
                logger => logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            await KafkaUtil.DeleteTopicAsync(testTopic);
        }
    }

    [Fact]
    public async Task ProduceAsync_BatchMessages_WithCancellationBeforeScheduling_ReturnsAllMessagesAndIncrementsFailedCounter()
    {
        // Arrange
        var failedCounterDelta = 0;
        var publishedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var validBatch = ImmutableList.Create("message1", "message2", "message3", "message4", "message5");

        using var producer = CreateTestProducer(loggerMock.Object);
        using var failedListener = CreateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));
        using var publishedListener = CreatePublishedCounterListener(delta => Interlocked.Add(ref publishedCounterDelta, delta));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var result = await producer.ProduceAsync(_batchTopic, validBatch, cancellationTokenSource.Token);
        var success = SpinWait.SpinUntil(() => Volatile.Read(ref failedCounterDelta) == validBatch.Count, TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.Same(validBatch, result);
        Assert.Equal(0, publishedCounterDelta);
        Assert.Equal(validBatch.Count, failedCounterDelta);
        Assert.True(success, $"Counter did not reach expected value {validBatch.Count} within timeout");

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Cancellation Requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Creates a test Kafka producer instance with default topic configuration.
    /// </summary>
    /// <param name="logger">The logger instance to inject into the producer.</param>
    /// <returns>A configured <see cref="KafkaProducer"/> instance for testing.</returns>
    private KafkaProducer CreateTestProducer(ILogger<KafkaProducer> logger)
    {
        var settings = CreateKafkaSettings(_validTopic, _batchTopic);
        return new KafkaProducer(Options.Create(settings), logger);
    }

    /// <summary>
    /// Creates Kafka settings with the specified topics configured for testing.
    /// </summary>
    /// <param name="topics">The topic names to include in the admin settings.</param>
    /// <returns>A configured <see cref="KafkaSettings"/> instance for testing.</returns>
    private static KafkaSettings CreateKafkaSettings(params string[] topics)
    {
        return new KafkaSettings
        {
            BrokerAddress = _brokerAddress,
            Admin = new AdminSettings { TopicList = [.. topics] }
        };
    }

    /// <summary>
    /// Injects a mock producer into the KafkaProducer instance using reflection for testing.
    /// </summary>
    /// <param name="producer">The KafkaProducer instance to modify.</param>
    /// <param name="mockProducer">The mock producer to inject.</param>
    /// <exception cref="InvalidOperationException">Thrown when the internal producer field cannot be found.</exception>
    private static void InjectMockProducer(KafkaProducer producer, IProducer<Null, string> mockProducer)
    {
        var fieldInfo = typeof(KafkaProducer).GetField("_producer", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_producer field not found");
        fieldInfo.SetValue(producer, mockProducer);
    }

    /// <summary>
    /// Creates a meter listener for tracking failed message counter metrics.
    /// </summary>
    /// <param name="onIncrement">Callback invoked when the failed counter is incremented.</param>
    /// <returns>A configured <see cref="MeterListener"/> for the kafka.producer.failed instrument.</returns>
    private static MeterListener CreateFailedCounterListener(Action<int> onIncrement)
    {
        return CreateCounterListener("kafka.producer.failed", onIncrement);
    }

    /// <summary>
    /// Creates a meter listener for tracking published message counter metrics.
    /// </summary>
    /// <param name="onIncrement">Callback invoked when the published counter is incremented.</param>
    /// <returns>A configured <see cref="MeterListener"/> for the kafka.producer.published instrument.</returns>
    private static MeterListener CreatePublishedCounterListener(Action<int> onIncrement)
    {
        return CreateCounterListener("kafka.producer.published", onIncrement);
    }

    /// <summary>
    /// Creates a meter listener for a specific counter instrument.
    /// </summary>
    /// <param name="instrumentName">The name of the counter instrument to listen for.</param>
    /// <param name="onMeasurement">Callback invoked when measurements are recorded.</param>
    /// <returns>A started <see cref="MeterListener"/> configured for the specified instrument.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onMeasurement"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="instrumentName"/> is null or whitespace.</exception>
    private static MeterListener CreateCounterListener(string instrumentName, Action<int> onMeasurement)
    {
        ArgumentNullException.ThrowIfNull(onMeasurement);
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentName);

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, subscription) =>
            {
                if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == instrumentName)
                {
                    subscription.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == instrumentName && measurement > 0)
            {
                onMeasurement(measurement);
            }
        });

        listener.Start();
        return listener;
    }

    /// <summary>
    /// Creates a meter listener for tracking latency measurements.
    /// </summary>
    /// <param name="onMeasurement">Callback invoked when latency measurements are recorded.</param>
    /// <returns>A started <see cref="MeterListener"/> configured for the kafka.producer.single.latency.ms instrument.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onMeasurement"/> is null.</exception>
    private static MeterListener CreateLatencyListener(Action<double> onMeasurement)
    {
        ArgumentNullException.ThrowIfNull(onMeasurement);

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, subscription) =>
            {
                if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == "kafka.producer.single.latency.ms")
                {
                    subscription.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == "kafka.producer.single.latency.ms")
            {
                onMeasurement(value);
            }
        });

        listener.Start();
        return listener;
    }
}
