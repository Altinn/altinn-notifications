using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.Kafka.Extensions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Kafka producer that publishes messages to configured topics using Confluent.Kafka.
/// Supports both single message and batch message publishing with comprehensive error handling,
/// metrics tracking, and automatic topic creation. Implements the disposable pattern for proper resource cleanup.
/// </summary>
public class KafkaProducer : SharedClientConfig, IKafkaProducer, IDisposable
{
    private readonly KafkaSettings _kafkaSettings;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly IProducer<Null, string> _producer;

    private static readonly Meter _meter = new("Altinn.Notifications.KafkaProducer", "1.0.0");
    private static readonly Counter<int> _failedCounter = _meter.CreateCounter<int>("kafka.producer.failed");
    private static readonly Counter<int> _publishedCounter = _meter.CreateCounter<int>("kafka.producer.published");
    private static readonly Histogram<double> _batchLatencyMs = _meter.CreateHistogram<double>("kafka.producer.batch.latency.ms");
    private static readonly Histogram<double> _singleLatencyMs = _meter.CreateHistogram<double>("kafka.producer.single.latency.ms");

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaProducer"/> class.
    /// </summary>
    public KafkaProducer(IOptions<KafkaSettings> kafkaSettings, ILogger<KafkaProducer> logger)
        : base(kafkaSettings.Value)
    {
        _logger = logger;
        _kafkaSettings = kafkaSettings.Value;

        var producerConfiguration = BuildConfiguration();

        _producer = BuildProducer(producerConfiguration);

        EnsureTopicsExist().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topicName, string message)
    {
        if (!ValidateTopic(topicName))
        {
            IncrementFailed(topicName);

            return false;
        }

        if (!ValidateMessage(message))
        {
            IncrementFailed(topicName);

            return false;
        }

        var produceStopwatch = Stopwatch.StartNew();

        try
        {
            var produceResult = await _producer.ProduceAsync(topicName, new Message<Null, string> { Value = message });

            if (produceResult.Status == PersistenceStatus.Persisted)
            {
                IncrementPublished(topicName);

                return true;
            }

            _logger.LogError("// KafkaProducer // ProduceAsync // Message not persisted. Status={Status}", produceResult.Status);

            IncrementFailed(topicName);

            return false;
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(
                ex,
                "// KafkaProducer // ProduceAsync // ProduceException Code={Code} Reason={Reason}",
                ex.Error.Code,
                ex.Error.Reason);

            IncrementFailed(topicName);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Unexpected exception.");

            IncrementFailed(topicName);

            return false;
        }
        finally
        {
            produceStopwatch.Stop();

            _singleLatencyMs.Record(produceStopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("topic", topicName));
        }
    }

    /// <inheritdoc/>
    public async Task<ImmutableList<string>> ProduceAsync(string topicName, ImmutableList<string> messages, CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // No messages to produce");

            return messages;
        }

        if (!ValidateTopic(topicName))
        {
            IncrementFailed(topicName, messages.Count);

            return messages;
        }

        BatchProducingContext batchContext = CategorizeMessages(topicName, messages);
        if (batchContext.ValidMessages.Count == 0)
        {
            return messages;
        }

        batchContext = BuildProduceTasks(topicName, batchContext, cancellationToken);
        if (batchContext.DeferredProduceTasks.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            IncrementFailed(topicName, batchContext.ValidMessages.Count);

            return messages;
        }

        var batchProcessingStopwatch = Stopwatch.StartNew();

        var batchFailed = false;
        var batchCategorized = false;

        var deliveryTasks = new List<Task<DeliveryResult<Null, string>>>(batchContext.DeferredProduceTasks.Count);

        try
        {
            deliveryTasks.AddRange(batchContext.DeferredProduceTasks.Select(e => e.ProduceTask()));

            await Task.WhenAll(deliveryTasks);

            batchContext = CategorizeDeliveryResults(topicName, batchContext, deliveryTasks);

            batchCategorized = true;

            batchFailed = batchContext.NotProducedMessages.Count > 0;
        }
        catch (Exception ex)
        {
            batchFailed = true;

            LogOnProducingFailures(ex);

            if (deliveryTasks.Count > 0 && !batchCategorized)
            {
                deliveryTasks = [.. deliveryTasks.Where(e => e.IsCompleted)];

                if (deliveryTasks.Count > 0)
                {
                    batchContext = CategorizeDeliveryResults(topicName, batchContext, deliveryTasks);
                }
            }

            // If still no classification, mark all valid as not produced
            if (batchContext.ValidMessages.Count > 0 &&
                batchContext.ProducedMessages.Count == 0 &&
                batchContext.NotProducedMessages.Count == 0)
            {
                batchContext = batchContext with
                {
                    NotProducedMessages = [.. batchContext.ValidMessages]
                };

                IncrementFailed(topicName, batchContext.ValidMessages.Count);
            }
        }
        finally
        {
            batchProcessingStopwatch.Stop();

            _batchLatencyMs.Record(
                batchProcessingStopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("topic", topicName),
                new KeyValuePair<string, object?>("batch.status", batchFailed ? "failed" : "succeeded"),
                new KeyValuePair<string, object?>("batch.valid.count", batchContext.ValidMessages.Count),
                new KeyValuePair<string, object?>("batch.invalid.count", batchContext.InvalidMessages.Count),
                new KeyValuePair<string, object?>("batch.produced.count", batchContext.ProducedMessages.Count),
                new KeyValuePair<string, object?>("batch.notproduced.count", batchContext.NotProducedMessages.Count));

            LogBatchResults(topicName, batchContext, batchProcessingStopwatch);
        }

        return [.. batchContext.InvalidMessages, .. batchContext.NotProducedMessages];
    }

    /// <summary>
    /// Disposes the producer instance and attempts a graceful flush.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs cleanup of managed and unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        try
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // Dispose // Flush failed.");
        }
        finally
        {
            _producer.Dispose();
        }
    }

    /// <summary>
    /// Ensures that required topics exist by creating any missing topics through the Kafka admin client.
    /// Topics are sourced from the configured topic list and created with predefined specifications.
    /// </summary>
    /// <exception cref="Exception">Thrown when metadata fetch fails or topic creation encounters unrecoverable errors.</exception>
    private async Task EnsureTopicsExist()
    {
        using var adminClient = new AdminClientBuilder(AdminClientSettings).Build();

        Metadata metadata;
        try
        {
            metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            LogOnMetadataFetchFailed(ex);

            throw;
        }

        var existingTopics = new HashSet<string>(metadata.Topics.Select(e => e.Topic), StringComparer.OrdinalIgnoreCase);

        foreach (string topic in _kafkaSettings.Admin.TopicList)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                _logger.LogWarning("// KafkaProducer // EnsureTopicsExist // Skipping empty topic.");

                continue;
            }

            if (existingTopics.Contains(topic))
            {
                continue;
            }

            try
            {
                await adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = topic,
                        Configs = TopicSpecification.Configs,
                        NumPartitions = TopicSpecification.NumPartitions,
                        ReplicationFactor = TopicSpecification.ReplicationFactor
                    }
                ]);

                _logger.LogInformation("// KafkaProducer // EnsureTopicsExist // Created topic '{Topic}'", topic);
            }
            catch (CreateTopicsException ex)
            {
                if (ex.Results.Any(e => e.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    continue;
                }

                LogOnTopicCreationFailed(ex, topic);

                throw;
            }
        }
    }

    /// <summary>
    /// Validates that the provided topic name is non-null, non-whitespace and found in the configured topics list.
    /// </summary>
    /// <param name="topic">The topic name to validate.</param>
    /// <returns><c>true</c> if the topic is valid; otherwise, <c>false</c>.</returns>
    private bool ValidateTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // Topic name is null, empty or whitespace");

            return false;
        }

        if (!_kafkaSettings.Admin.TopicList.Contains(topic, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // Topic name is not found in the list of topics.");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds producer configuration with optimized settings for reliable, high-performance message delivery.
    /// </summary>
    /// <returns>A configured <see cref="ProducerConfig"/> instance ready for production use.</returns>
    private ProducerConfig BuildConfiguration()
    {
        return new(ProducerSettings)
        {
            LingerMs = 50,
            Acks = Acks.All,
            MaxInFlight = 5,
            RetryBackoffMs = 250,
            BatchSize = 1024 * 1024,
            EnableIdempotence = true,
            EnableBackgroundPoll = true,
            SocketKeepaliveEnable = true,
            EnableDeliveryReports = true,
            StatisticsIntervalMs = 10000,
            DeliveryReportFields = "status",
            CompressionType = CompressionType.Zstd,
            Partitioner = Partitioner.ConsistentRandom
        };
    }

    /// <summary>
    /// Validates that the provided message content is non-null and non-whitespace.
    /// </summary>
    /// <param name="message">The message content to validate.</param>
    /// <returns><c>true</c> if the message is valid; otherwise, <c>false</c>.</returns>
    private bool ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // Message is null, empty, or whitespace");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Logs the error that occurred while producing messages to the Kafka topic during batch processing.
    /// </summary>
    /// <param name="exception">The exception that was thrown during batch message production.</param>
    private void LogOnProducingFailures(Exception exception)
    {
        _logger.LogError(exception, "// KafkaProducer // ProduceAsync // Unexpected exception while awaiting batch.");
    }

    /// <summary>
    /// Logs the error that occurred while fetching Kafka metadata during topic existence validation.
    /// </summary>
    /// <param name="exception">The exception that was thrown during metadata retrieval.</param>
    private void LogOnMetadataFetchFailed(Exception exception)
    {
        _logger.LogError(exception, "// KafkaProducer // EnsureTopicsExist // Metadata fetch failed.");
    }

    /// <summary>
    /// Creates a Kafka producer instance with error and statistics handlers, plus instrumentation support.
    /// </summary>
    /// <param name="config">The producer configuration to use for creating the Kafka producer.</param>
    /// <returns>A configured <see cref="IProducer{TKey, TValue}"/> instance with attached event handlers and instrumentation.</returns>
    private IProducer<Null, string> BuildProducer(ProducerConfig config)
    {
        return new ProducerBuilder<Null, string>(config)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                {
                    _logger.LogCritical("// KafkaProducer // Fatal error {Code}: {Reason}", e.Code, e.Reason);
                }
                else if (e.IsError)
                {
                    _logger.LogError("// KafkaProducer // Broker error {Code}: {Reason}", e.Code, e.Reason);
                }
                else
                {
                    _logger.LogWarning("// KafkaProducer // Broker notice {Code}: {Reason}", e.Code, e.Reason);
                }
            })
            .SetStatisticsHandler((_, json) =>
            {
                _logger.LogDebug("// KafkaProducer // Stats: {StatsJson}", json);
            })
            .BuildWithInstrumentation();
    }

    /// <summary>
    /// Increments the failed message counter for metrics tracking.
    /// Updates the "kafka.producer.failed" counter with topic-specific tagging.
    /// </summary>
    /// <param name="topic">The Kafka topic name where the failure occurred.</param>
    /// <param name="failuresCount">The number of failed message deliveries to record. Defaults to 1.</param>
    private static void IncrementFailed(string topic, int failuresCount = 1)
    {
        _failedCounter.Add(failuresCount, KeyValuePair.Create<string, object?>("topic", topic));
    }

    /// <summary>
    /// Increments the published message counter for metrics tracking.
    /// Updates the "kafka.producer.published" counter with topic-specific tagging.
    /// </summary>
    /// <param name="topic">The Kafka topic name where messages were successfully published.</param>
    /// <param name="successesCount">The number of successful message deliveries to record. Defaults to 1.</param>
    private static void IncrementPublished(string topic, int successesCount = 1)
    {
        _publishedCounter.Add(successesCount, KeyValuePair.Create<string, object?>("topic", topic));
    }

    /// <summary>
    /// Logs the error that occurred while creating missing topics during startup initialization.
    /// </summary>
    /// <param name="exception">The exception that was thrown during topic creation.</param>
    /// <param name="topicName">The name of the topic that failed to be created.</param>
    private void LogOnTopicCreationFailed(Exception exception, string topicName)
    {
        _logger.LogError(exception, "// KafkaProducer // EnsureTopicsExist // Failed to create topic '{Topic}'", topicName);
    }

    /// <summary>
    /// Creates a <see cref="BatchProducingContext"/> containing the messages categorized into valid and invalid categories.
    /// </summary>
    /// <param name="topicName">The Kafka topic name for metrics tracking and logging context.</param>
    /// <param name="messages">The message collection to validate and categorize.</param>
    /// <returns>
    /// A <see cref="BatchProducingContext"/> containing messages categorized into valid and invalid categories.
    /// Valid messages are non-null, non-empty, and non-whitespace strings. Invalid messages include null, empty, or whitespace-only strings.
    /// </returns>
    private BatchProducingContext CategorizeMessages(string topicName, ImmutableList<string> messages)
    {
        var validMessages = new List<string>(messages.Count);
        var invalidMessages = new List<string>(messages.Count);

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                invalidMessages.Add(message ?? string.Empty);
            }
            else
            {
                validMessages.Add(message);
            }
        }

        if (validMessages.Count == 0)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // CategorizeMessages // No valid messages to publish");

            IncrementFailed(topicName, messages.Count);
        }
        else if (invalidMessages.Count > 0)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // CategorizeMessages // Cannot publish {Count} invalid messages", invalidMessages.Count);

            IncrementFailed(topicName, invalidMessages.Count);
        }

        return new BatchProducingContext
        {
            ValidMessages = [.. validMessages],
            InvalidMessages = [.. invalidMessages]
        };
    }

    /// <summary>
    /// Logs performance metrics and success rates for batch message production.
    /// </summary>
    /// <param name="topicName">The topic name targeted by this batch operation.</param>
    /// <param name="batchContext">The batch context containing processing results and statistics.</param>
    /// <param name="batchStopwatch">The stopwatch containing the total batch processing duration.</param>
    private void LogBatchResults(string topicName, BatchProducingContext batchContext, Stopwatch batchStopwatch)
    {
        var successRate = batchContext.ValidMessages.Count == 0
            ? 0
            : (double)batchContext.ProducedMessages.Count / batchContext.ValidMessages.Count;

        _logger.LogInformation(
            "// KafkaProducer // ProduceAsync // Topic={Topic} TotalValid={TotalValid} Produced={ProducedCount} NotProduced={NotProducedCount} SuccessRate={Rate:P2} DurationMs={Duration:F0}",
            topicName,
            batchContext.ValidMessages.Count,
            batchContext.ProducedMessages.Count,
            batchContext.NotProducedMessages.Count,
            successRate,
            batchStopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Creates a <see cref="BatchProducingContext"/> with deferred produce task factories for each valid message.
    /// Respects cancellation requests during task creation, stopping early if cancellation is requested.
    /// </summary>
    /// <param name="topicName">The target Kafka topic name for message production.</param>
    /// <param name="batchContext">The batch processing context containing categorized valid and invalid messages.</param>
    /// <param name="cancellationToken">A cancellation token that can interrupt the scheduling process.</param>
    /// <returns>
    /// An updated <see cref="BatchProducingContext"/> with a list populated with task factories for scheduled messages.
    /// </returns>
    private BatchProducingContext BuildProduceTasks(string topicName, BatchProducingContext batchContext, CancellationToken cancellationToken)
    {
        var scheduledMessagesCount = 0;
        var unscheduledMessagesCount = batchContext.ValidMessages.Count;

        List<ProduceTaskFactory> produceTaskFactories = [];
        foreach (var validMessage in batchContext.ValidMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "// KafkaProducer // ProduceAsync // BuildProduceTasks // Cancellation Requested. Topic={Topic} Scheduled={Scheduled} Unscheduled={Unscheduled}",
                    topicName,
                    scheduledMessagesCount,
                    unscheduledMessagesCount);

                break;
            }

            // Capture the message in a local variable to avoid closure issues
            var messagePayload = validMessage;

            produceTaskFactories.Add(ProduceTaskFactory.Create(topicName, messagePayload, _producer, cancellationToken));

            scheduledMessagesCount++;
            unscheduledMessagesCount--;
        }

        return batchContext with
        {
            DeferredProduceTasks = [.. produceTaskFactories]
        };
    }

    /// <summary>
    /// Creates a <see cref="BatchProducingContext"/> with delivery task results categorized into produced and not produced groups.
    /// </summary>
    /// <param name="topicName">The Kafka topic name where messages were produced for metrics tracking.</param>
    /// <param name="batchContext">The batch processing context containing deferred produce tasks to pair with delivery results.</param>
    /// <param name="deliveryTasks">Collection of completed delivery tasks returned by Kafka <c>ProduceAsync</c> operations.</param>
    /// <returns>
    /// Updated <see cref="BatchProducingContext"/> with messages categorized based on delivery results.
    /// Successfully persisted messages are added to <see cref="BatchProducingContext.ProducedMessages"/>.
    /// Failed, non-persisted, canceled, or faulted messages are added to <see cref="BatchProducingContext.NotProducedMessages"/>.
    /// Metrics counters are updated accordingly for each message outcome.
    /// </returns>
    private static BatchProducingContext CategorizeDeliveryResults(string topicName, BatchProducingContext batchContext, List<Task<DeliveryResult<Null, string>>> deliveryTasks)
    {
        var producedMessages = new List<string>(batchContext.ValidMessages.Count);
        var notProducedMessages = new List<string>(batchContext.ValidMessages.Count);

        var scheduledPayloads = batchContext.DeferredProduceTasks.Select(e => e.Message).ToList();

        var pairedCount = Math.Min(deliveryTasks.Count, scheduledPayloads.Count);

        for (int i = 0; i < pairedCount; i++)
        {
            var deliveryTask = deliveryTasks[i];
            var deliveryTaskMessage = scheduledPayloads[i];

            if (deliveryTask.IsCanceled || deliveryTask.IsFaulted)
            {
                if (!string.IsNullOrWhiteSpace(deliveryTaskMessage))
                {
                    notProducedMessages.Add(deliveryTaskMessage);
                }

                IncrementFailed(topicName);

                continue;
            }

            var deliveryTaskResult = deliveryTask.Result;

            if (string.IsNullOrWhiteSpace(deliveryTaskMessage))
            {
                deliveryTaskMessage = deliveryTaskResult.Message?.Value ?? deliveryTaskResult.Value;
            }

            if (string.IsNullOrWhiteSpace(deliveryTaskMessage))
            {
                IncrementFailed(topicName);

                continue;
            }

            if (deliveryTaskResult.Status == PersistenceStatus.Persisted)
            {
                producedMessages.Add(deliveryTaskMessage);

                IncrementPublished(topicName);
            }
            else
            {
                notProducedMessages.Add(deliveryTaskMessage);

                IncrementFailed(topicName);
            }
        }

        // If there were more scheduled messages than completed tasks, mark remaining as not produced.
        if (scheduledPayloads.Count > pairedCount)
        {
            var remainingMessages = scheduledPayloads.Skip(pairedCount).Where(e => !string.IsNullOrWhiteSpace(e));

            foreach (var remainingMessage in remainingMessages)
            {
                notProducedMessages.Add(remainingMessage);

                IncrementFailed(topicName);
            }
        }

        return batchContext with
        {
            ProducedMessages = [.. producedMessages],
            NotProducedMessages = [.. notProducedMessages]
        };
    }
}
