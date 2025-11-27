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

        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureTopicsExist().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Could not validate topics.");
            }
        });
    }

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topicName, string message)
    {
        if (!ValidateTopic(topicName))
        {
            return false;
        }

        if (!ValidateMessage(message))
        {
            IncrementFailed(topicName);

            return false;
        }

        var produceStartTime = Stopwatch.StartNew();

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
            produceStartTime.Stop();

            _singleLatencyMs.Record(produceStartTime.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> ProduceAsync(string topicName, IImmutableList<string> messages, CancellationToken cancellationToken = default)
    {
        if (!ValidateTopic(topicName))
        {
            return messages;
        }

        if (messages.Count == 0)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // No messages to produce");

            return messages;
        }

        BatchProducingContext batchContext = CategorizeMessages(topicName, messages);
        if (batchContext.ValidMessages.Count == 0)
        {
            return messages;
        }

        batchContext = BuildProduceTasks(topicName, batchContext, cancellationToken);
        if (batchContext.DeferredProduceTaskFactories.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            return messages;
        }

        var batchProcessingStopwatch = Stopwatch.StartNew();

        try
        {
            var publishDeliveryResults = batchContext.DeferredProduceTaskFactories.AsParallel().Select(factory => factory()).ToList();

            await Task.WhenAll(publishDeliveryResults);

            batchContext = CategorizeDeliveryResults(topicName, batchContext, publishDeliveryResults);
        }
        catch (Exception ex)
        {
            LogOnProducingFailures(ex);

            throw;
        }
        finally
        {
            batchProcessingStopwatch.Stop();
        }

        _batchLatencyMs.Record(batchProcessingStopwatch.Elapsed.TotalMilliseconds);

        LogBatchResults(topicName, batchContext, batchProcessingStopwatch);

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
    /// </summary>
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
    /// Validates that the provided topic name is non-null, non-whitespace and found in the topics list.
    /// </summary>
    private bool ValidateTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // Topic name is null, empty or whitespace");

            return false;
        }

        if (!_kafkaSettings.Admin.TopicList.Contains(topic))
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // Topic name is not found in the list of topics.");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds producer settings used by the Kafka producer instance.
    /// </summary>
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
            CompressionType = CompressionType.Zstd,
            DeliveryReportFields = "key,value,status",
            Partitioner = Partitioner.ConsistentRandom
        };
    }

    /// <summary>
    /// Validates that the provided message content is non-null and non-whitespace.
    /// </summary>
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
    /// Logs the error that occurred while producing messages to the Kafka topic.
    /// </summary>
    /// <param name="exception">The exception occurred.</param>
    private void LogOnProducingFailures(Exception exception)
    {
        _logger.LogCritical(exception, "// KafkaProducer // ProduceAsync // Unexpected exception while awaiting batch.");
    }

    /// <summary>
    /// Logs the error that occurred while fetching Kafka metadata.
    /// </summary>
    /// <param name="exception">The exception occurred.</param>
    private void LogOnMetadataFetchFailed(Exception exception)
    {
        _logger.LogError(exception, "// KafkaProducer // EnsureTopicsExist // Metadata fetch failed.");
    }

    /// <summary>
    /// Creates a Kafka producer instance and attaches error and statistics handlers.
    /// </summary>
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
    /// </summary>
    /// <param name="topic">The Kafka topic name where the failure occurred.</param>
    /// <param name="failuresCount">The number of failed message deliveries to record.</param>
    private static void IncrementFailed(string topic, int failuresCount = 1)
    {
        _failedCounter.Add(failuresCount, KeyValuePair.Create<string, object?>("topic", topic));
    }

    /// <summary>
    /// Increments the published message counter for metrics tracking.
    /// </summary>
    /// <param name="topic">The Kafka topic name where messages were successfully published.</param>
    /// <param name="successesCount">The number of successful message deliveries to record.</param>
    private static void IncrementPublished(string topic, int successesCount = 1)
    {
        _publishedCounter.Add(successesCount, KeyValuePair.Create<string, object?>("topic", topic));
    }

    /// <summary>
    /// Logs the error that occurred while creating missing topics.
    /// </summary>
    /// <param name="exception">The exception occurred.</param>
    /// <param name="topicName">The name of the topic.</param>
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
    private BatchProducingContext CategorizeMessages(string topicName, IImmutableList<string> messages)
    {
        var validMessages = new List<string>(messages.Count);
        var invalidMessages = new List<string>(messages.Count);

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                invalidMessages.Add(message);
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

        if (invalidMessages.Count > 0)
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
    /// Logs performance metrics and success rates.
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
    /// </summary>
    /// <param name="topicName">The target Kafka topic name for message production.</param>
    /// <param name="batchContext">The batch processing context containing categorized valid and invalid messages.</param>
    /// <param name="cancellationToken">A cancellation token that can interrupt the scheduling process.</param>
    /// <returns>
    /// An updated <see cref="BatchProducingContext"/> with a list populated with task factories for scheduled messages
    /// Each task factory captures the message payload to prevent closure variable issues and returns a <see cref="Task{DeliveryResult}"/> when executed.
    /// </returns>
    private BatchProducingContext BuildProduceTasks(string topicName, BatchProducingContext batchContext, CancellationToken cancellationToken)
    {
        var unscheduledMessagesCount = batchContext.ValidMessages.Count;
        if (unscheduledMessagesCount == 0)
        {
            return batchContext;
        }

        var scheduledMessagesCount = 0;
        var deferredProduceTaskFactories = new List<Func<Task<DeliveryResult<Null, string>>>>(unscheduledMessagesCount);

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

            deferredProduceTaskFactories.Add(() => _producer.ProduceAsync(topicName, new Message<Null, string> { Value = messagePayload }));

            scheduledMessagesCount++;
            unscheduledMessagesCount--;
        }

        return batchContext with
        {
            DeferredProduceTaskFactories = [.. deferredProduceTaskFactories]
        };
    }

    /// <summary>
    /// Creates a <see cref="BatchProducingContext"/> with delivery task results categorizes into produced and not produced groups.
    /// </summary>
    /// <param name="topicName">The Kafka topic name where messages were produced to.</param>
    /// <param name="batchContext">The batch processing context to update with delivery results.</param>
    /// <param name="deliveryTasks">Collection of delivery tasks returned by Kafka <c>ProduceAsync</c> operations.</param>
    /// <returns>
    /// Updated <see cref="BatchProducingContext"/> with messages categorized based on delivery results.
    /// Successfully persisted messages are added to <see cref="BatchProducingContext.ProducedMessages"/>.
    /// Failed or non-persisted messages are added to <see cref="BatchProducingContext.NotProducedMessages"/>.
    /// </returns>
    private static BatchProducingContext CategorizeDeliveryResults(string topicName, BatchProducingContext batchContext, List<Task<DeliveryResult<Null, string>>> deliveryTasks)
    {
        var producedMessages = new List<string>(batchContext.ValidMessages.Count);
        var notProducedMessages = new List<string>(batchContext.ValidMessages.Count);

        foreach (var deliveryTask in deliveryTasks)
        {
            if (deliveryTask.IsFaulted)
            {
                var produceException = deliveryTask.Exception?
                    .Flatten()
                    .InnerExceptions
                    .OfType<ProduceException<Null, string>>()
                    .FirstOrDefault();

                var failedPayload = produceException?.DeliveryResult?.Message?.Value ?? produceException?.DeliveryResult?.Value;

                if (!string.IsNullOrWhiteSpace(failedPayload))
                {
                    notProducedMessages.Add(failedPayload);
                }

                IncrementFailed(topicName);

                continue;
            }

            var deliveryTaskResult = deliveryTask.Result;
            var payload = deliveryTaskResult.Message?.Value ?? deliveryTaskResult.Value;

            if (deliveryTaskResult.Status == PersistenceStatus.Persisted)
            {
                producedMessages.Add(payload);

                IncrementPublished(topicName);
            }
            else
            {
                notProducedMessages.Add(payload);

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
