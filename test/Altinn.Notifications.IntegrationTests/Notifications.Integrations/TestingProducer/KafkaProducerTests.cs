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
    private readonly string _brokerAddress = "localhost:9092";
    private readonly string _emptyTopic = $"kafka-producer-empty-{Guid.NewGuid():N}";
    private readonly string _invalidTopic = $"kafka-producer-invalid-{Guid.NewGuid():N}";
    private readonly string _allInvalidBatchTopic = $"kafka-producer-invalid-{Guid.NewGuid():N}";

    /// <summary>
    /// Disposes the class instance, after all tests have been run.
    /// </summary>
    public async Task DisposeAsync()
    {
        _sharedProducer?.Dispose();

        await KafkaUtil.DeleteTopicAsync(_emptyTopic);
        await KafkaUtil.DeleteTopicAsync(_invalidTopic);
        await KafkaUtil.DeleteTopicAsync(_allInvalidBatchTopic);
    }

    /// <summary>
    /// Initializes the class instance, creating the necessary Kafka topics.
    /// </summary>
    public async Task InitializeAsync()
    {
        await KafkaUtil.CreateTopicAsync(_emptyTopic);
        await KafkaUtil.CreateTopicAsync(_allInvalidBatchTopic);

        var settings = new KafkaSettings
        {
            BrokerAddress = _brokerAddress,
            Admin = new AdminSettings { TopicList = { _emptyTopic, _allInvalidBatchTopic } }
        };

        var nullLogger = Mock.Of<ILogger<KafkaProducer>>();
        _sharedProducer = new KafkaProducer(Options.Create(settings), nullLogger);
    }
    
    [Fact]
    public async Task ProduceAsync_WithInvalidSingleMessage_ReturnsFalseAndIncrementsFailed()
    {
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        using var producer = CreateTestProducerWithLogger(loggerMock);

        var result = await producer.ProduceAsync(_emptyTopic, "   ");

        Assert.False(result);
        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => s.ToString()!.Contains("Message is null, empty, or whitespace")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ProduceAsync_WithSingleMessagePersisted_ReturnsTrueAndPublishedIncrement()
    {
        // Arrange
        int publishedCounterDelta = 0;
        var messageToPublish = "new-message";
        var loggerMock = new Mock<ILogger<KafkaProducer>>();

        using var producer = CreateTestProducerWithLogger(loggerMock);

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, e) =>
            {
                if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == "kafka.producer.published")
                {
                    e.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == "kafka.producer.published")
            {
                Interlocked.Add(ref publishedCounterDelta, measurement);
            }
        });

        listener.Start();

        var mockInnerProducer = new Mock<IProducer<Null, string>>();
        mockInnerProducer
            .Setup(e => e.ProduceAsync(_emptyTopic, It.Is<Message<Null, string>>(m => m.Value == messageToPublish), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<Null, string>
            {
                Status = PersistenceStatus.Persisted,
                Message = new Message<Null, string> { Value = messageToPublish }
            });

        var field = typeof(KafkaProducer).GetField("_producer", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("_producer field not found.");
        field.SetValue(producer, mockInnerProducer.Object);

        // Act
        var result = await producer.ProduceAsync(_emptyTopic, messageToPublish);
        await Task.Delay(25); // allow meter to emit

        // Assert
        Assert.True(result);
        loggerMock.VerifyNoOtherCalls();
        Assert.Equal(1, publishedCounterDelta);
    }

    [Fact]
    public async Task ProduceAsync_WithInvalidTopicForSingleMessage_ReturnsFalseAndIncrementsFailed()
    {
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        using var producer = CreateTestProducerWithLogger(loggerMock);

        var result = await producer.ProduceAsync(_invalidTopic, "payload");

        Assert.False(result);
        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => s.ToString()!.Contains("Topic name is not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_WithInvalidMessagesBatch_CategorizeEarlyReturn()
    {
        int failedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var invalidMessagesBatch = ImmutableList.Create(null!, string.Empty, " ");

        using var producer = CreateTestProducerWithLogger(loggerMock);

        using var listener = InitiateFailedCounterListener(increment => Interlocked.Add(ref failedCounterDelta, increment));

        var result = await producer.ProduceAsync(_allInvalidBatchTopic, invalidMessagesBatch);
        await Task.Delay(25); // Allow some time for the meter to emit measurements

        Assert.Equal(invalidMessagesBatch, result);
        Assert.Equal(invalidMessagesBatch.Count, failedCounterDelta);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProduceAsync_WithEmptyMessagesBatch_ReturnsEmptyMessagesAndLogsError()
    {
        var loggerMock = new Mock<ILogger<KafkaProducer>>();

        var emptyMessagesBatch = ImmutableList<string>.Empty;

        using var producer = CreateTestProducerWithLogger(loggerMock);

        var result = await producer.ProduceAsync(_emptyTopic, emptyMessagesBatch);

        Assert.Empty(result);
        Assert.Same(emptyMessagesBatch, result);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_WithInvalidTopic_AllMessagesInTheBatchReportedFailedAndReturned()
    {
        int failedCounterDelta = 0;
        var invalidTopic = $"invalid-{Guid.NewGuid():N}";
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var validMessagesBatch = ImmutableList.Create("a", "b", "c");

        using var producer = CreateTestProducerWithLogger(loggerMock);

        using var listener = InitiateFailedCounterListener(increment => Interlocked.Add(ref failedCounterDelta, increment));

        var result = await producer.ProduceAsync(invalidTopic, validMessagesBatch);
        await Task.Delay(25); // Allow some time for the meter to emit measurements

        Assert.Equal(validMessagesBatch, result);
        Assert.Equal(validMessagesBatch.Count, failedCounterDelta);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_AllProduceTasksThrow_AllMessagesInTheBatchReturnedAsNotProduced()
    {
        int failedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var validMessagesBatch = ImmutableList.Create("x1", "x2", "x3");

        var localTopic = $"kafka-producer-all-throw-{Guid.NewGuid():N}";

        var settings = new KafkaSettings
        {
            BrokerAddress = _brokerAddress,
            Admin = new AdminSettings { TopicList = { localTopic } } // include so ValidateTopic passes
        };

        using var producer = new KafkaProducer(Options.Create(settings), loggerMock.Object);

        await KafkaUtil.CreateTopicAsync(localTopic);

        var field = typeof(KafkaProducer).GetField("_producer", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException("_producer field not found.");
        field.SetValue(producer, new FailingProducer());

        using var listener = InitiateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));

        var result = await producer.ProduceAsync(localTopic, validMessagesBatch);
        await Task.Delay(30);

        Assert.Equal(validMessagesBatch, result);
        Assert.Equal(validMessagesBatch.Count, failedCounterDelta);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        await KafkaUtil.DeleteTopicAsync(localTopic);
    }

    [Fact]
    public async Task ProduceAsync_WhenProduceTaskThrows_FallbackMarksAllValidAsNotProducedAndIncrementsFailed()
    {
        // Arrange
        int failedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var localTopic = $"kafka-producer-fallback-{Guid.NewGuid():N}";
        var validMessagesBatch = ImmutableList.Create("p1", "p2", "p3");

        await KafkaUtil.CreateTopicAsync(localTopic);

        var settings = new KafkaSettings
        {
            BrokerAddress = _brokerAddress,
            Admin = new AdminSettings { TopicList = { localTopic } }
        };

        using var producer = new KafkaProducer(Options.Create(settings), loggerMock.Object);

        var categorizeMethod = typeof(KafkaProducer).GetMethod("CategorizeMessages", BindingFlags.Instance | BindingFlags.NonPublic)
                               ?? throw new InvalidOperationException("CategorizeMessages not found.");

        var buildTasksMethod = typeof(KafkaProducer).GetMethod("BuildProduceTasks", BindingFlags.Instance | BindingFlags.NonPublic)
                               ?? throw new InvalidOperationException("BuildProduceTasks not found.");

        var execFinalizeMethod = typeof(KafkaProducer).GetMethod("ExecuteAndFinalizeBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                               ?? throw new InvalidOperationException("ExecuteAndFinalizeBatchAsync not found.");

        var batchContext = (BatchProducingContext)categorizeMethod.Invoke(producer, [localTopic, validMessagesBatch])!;
        batchContext = (BatchProducingContext)buildTasksMethod.Invoke(producer, [localTopic, batchContext, CancellationToken.None])!;

        var throwingFactories = validMessagesBatch
            .Select(msg => new ProduceTaskFactory
            {
                Message = msg,
                ProduceTask = () => throw new InvalidOperationException("Synchronous factory invocation failure")
            })
            .ToImmutableList();

        batchContext = batchContext with { DeferredProduceTasks = throwingFactories };

        using var listener = InitiateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));

        // Act
        var execTask = (Task<BatchProducingContext>)execFinalizeMethod.Invoke(producer, new object[] { localTopic, batchContext })!;
        var finalizedContext = await execTask;

        // Assert
        Assert.Equal(validMessagesBatch.Count, failedCounterDelta);
        Assert.Equal(validMessagesBatch, finalizedContext.NotProducedMessages);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        await KafkaUtil.DeleteTopicAsync(localTopic);
    }

    [Fact]
    public async Task ProduceAsync_WithCancellationRequestedBeforeScheduling_AllMessagesInTheBatchReturnedAndIncrementsFailed()
    {
        // Arrange
        int failedCounterDelta = 0;
        var loggerMock = new Mock<ILogger<KafkaProducer>>();
        var validMessagesBatch = ImmutableList.Create("m1", "m2", "m3", "m4", "m5");

        using var producer = CreateTestProducerWithLogger(loggerMock);

        using var listener = InitiateFailedCounterListener(delta => Interlocked.Add(ref failedCounterDelta, delta));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync(); // Cancellation requested before scheduling produce tasks

        // Act
        var result = await producer.ProduceAsync(_emptyTopic, validMessagesBatch, cancellationTokenSource.Token);
        await Task.Delay(25); // allow metrics callback

        // Assert
        Assert.Same(validMessagesBatch, result);
        Assert.Equal(validMessagesBatch.Count, failedCounterDelta);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Creates and starts a <see cref="MeterListener"/> subscribed to the Kafka producer failed counter
    /// instrument (<c>"kafka.producer.failed"</c>) emitted by the <c>"Altinn.Notifications.KafkaProducer"</c> meter.
    /// Invokes the provided <paramref name="onIncrement"/> callback for each positive measurement observed.
    /// </summary>
    /// <param name="onIncrement">
    /// Callback invoked with the delta value of each failed publish count (always &gt; 0).
    /// The callback can be invoked from arbitrary threads; it must be thread-safe.
    /// </param>
    /// <returns>
    /// A started <see cref="MeterListener"/> instance. Call <c>Dispose()</c> when you no longer need to observe measurements.
    /// </returns>
    private static MeterListener InitiateFailedCounterListener(Action<int> onIncrement)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, e) =>
            {
                if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == "kafka.producer.failed")
                {
                    e.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            if (instrument.Meter.Name == "Altinn.Notifications.KafkaProducer" && instrument.Name == "kafka.producer.failed")
            {
                onIncrement(measurement);
            }
        });

        listener.Start();

        return listener;
    }

    /// <summary>
    /// Creates a fresh <see cref="KafkaProducer"/> instance configured with the test topics
    /// and the supplied logger mock, enabling isolated verification of log output per test.
    /// Topics are assumed to be pre-created during <see cref="InitializeAsync"/>.
    /// </summary>
    /// <param name="loggerMock">Mock logger used to capture and assert log entries.</param>
    /// <returns>Disposable producer instance dedicated to a single test.</returns>
    private KafkaProducer CreateTestProducerWithLogger(Mock<ILogger<KafkaProducer>> loggerMock)
    {
        var settings = new KafkaSettings
        {
            BrokerAddress = _brokerAddress,
            Admin = new AdminSettings { TopicList = { _emptyTopic, _allInvalidBatchTopic } }
        };

        return new KafkaProducer(Options.Create(settings), loggerMock.Object);
    }
}
