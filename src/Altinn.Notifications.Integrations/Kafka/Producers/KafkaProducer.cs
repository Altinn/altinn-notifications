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

        _ = EnsureTopicsExist();
    }

    /// <inheritdoc/>
    public async Task<bool> ProduceAsync(string topic, string message)
    {
        if (!ValidateTopic(topic))
        {
            return false;
        }

        if (!ValidateMessage(message))
        {
            return false;
        }

        var produceStartTime = Stopwatch.StartNew();

        try
        {
            var produceResult = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = message });

            if (produceResult.Status == PersistenceStatus.Persisted)
            {
                IncrementPublished();

                return true;
            }

            _logger.LogError("// KafkaProducer // ProduceAsync // Message not persisted. Status={Status}", produceResult.Status);

            IncrementFailed();

            return false;
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(
                ex,
                "// KafkaProducer // ProduceAsync // ProduceException Code={Code} Reason={Reason}",
                ex.Error.Code,
                ex.Error.Reason);

            IncrementFailed();

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Unexpected exception.");

            IncrementFailed();

            return false;
        }
        finally
        {
            produceStartTime.Stop();

            _singleLatencyMs.Record(produceStartTime.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> ProduceAsync(string topic, IImmutableList<string> messages, CancellationToken cancellationToken = default)
    {
        if (!ValidateTopic(topic))
        {
            return messages;
        }

        if (messages.Count == 0)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // No messages to produce");

            return messages;
        }

        var batchProcessingStopwatch = Stopwatch.StartNew();

        var batchProducingContext = CategorizeMessages(messages);
        if (!batchProducingContext.ValidMessages.Any())
        {
            batchProcessingStopwatch.Stop();

            _logger.LogError("// KafkaProducer // ProduceAsync // All messages are invalid");

            return messages;
        }

        try
        {
            batchProducingContext = CreateDeferredProduceTaskFactories(topic, batchProducingContext, cancellationToken);
            if (batchProducingContext.DeferredProduceTaskFactories.Count == 0)
            {
                batchProcessingStopwatch.Stop();

                _logger.LogError("// KafkaProducer // ProduceAsync // No message will be produced");

                return messages;
            }

            var publishTasks = new List<Task<DeliveryResult<Null, string>>>(batchProducingContext.DeferredProduceTaskFactories.Count);
            foreach (var factory in batchProducingContext.DeferredProduceTaskFactories)
            {
                publishTasks.Add(factory());
            }

            await Task.WhenAll(publishTasks);

            batchProducingContext = CategorizeDeliveryResults(batchProducingContext, [.. publishTasks]);
        }
        catch (OperationCanceledException)
        {
            batchProcessingStopwatch.Stop();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            batchProcessingStopwatch.Stop();

            throw;
        }

        batchProcessingStopwatch.Stop();

        _batchLatencyMs.Record(batchProcessingStopwatch.Elapsed.TotalMilliseconds);

        LogBatchResults(topic, batchProducingContext, batchProcessingStopwatch);

        return [.. batchProducingContext.InvalidMessages, .. batchProducingContext.UnpublishedMessages];
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
            LingerMs = 100,
            Acks = Acks.All,
            MaxInFlight = 5,
            RetryBackoffMs = 250,
            BatchSize = 512 * 1024,
            EnableIdempotence = true,
            EnableBackgroundPoll = true,
            SocketKeepaliveEnable = true,
            EnableDeliveryReports = true,
            StatisticsIntervalMs = 10000,
            CompressionType = CompressionType.Zstd,
            DeliveryReportFields = "key,value,status"
        };
    }

    /// <summary>
    /// Validates that the provided message content is non-null and non-whitespace.
    /// </summary>1
    private bool ValidateMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        _logger.LogError("// KafkaProducer // ProduceAsync // Message is null, empty, or whitespace");

        IncrementFailed();

        return false;
    }

    /// <summary>
    /// Logs the error that occurred while fetching Kafka metadata in EnsureTopicsExist.
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
    /// Logs the error that occurred while creating missing topics in EnsureTopicsExist.
    /// </summary>
    /// <param name="exception">The exception occurred.</param>
    /// <param name="topicName">The name of the topic.</param>
    private void LogOnTopicCreationFailed(Exception exception, string topicName)
    {
        _logger.LogError(exception, "// KafkaProducer // EnsureTopicsExist // Failed to create topic '{Topic}'", topicName);
    }

    /// <summary>
    /// Validates and categorizes a collection of messages into valid and invalid groups.
    /// </summary>
    /// <param name="messages">The message collection to validate and categorize.</param>
    /// <returns>
    /// A <see cref="BatchProducingContext"/> containing validated messages separated into valid and invalid collections.
    /// Valid messages are non-null, non-empty, and non-whitespace strings. Invalid messages include null, empty, or whitespace-only strings.
    /// </returns>
    private BatchProducingContext CategorizeMessages(IImmutableList<string> messages)
    {
        var containSingleValidMessage = false;
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

                containSingleValidMessage = true;
            }
        }

        if (!containSingleValidMessage)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // InitializeBatchProducingContext // No valid messages to publish");
        }

        if (invalidMessages.Count > 0)
        {
            IncrementFailed(invalidMessages.Count);

            _logger.LogError("// KafkaProducer // ProduceAsync // InitializeBatchProducingContext // {Count} invalid messages", invalidMessages.Count);
        }

        return new BatchProducingContext
        {
            ValidMessages = [.. validMessages],
            InvalidMessages = [.. invalidMessages]
        };
    }

    /// <summary>
    /// Increments the failure counter, allowing batch operations to record multiple failures.
    /// </summary>
    private static void IncrementFailed(int failuresCount = 1) => _failedCounter.Add(failuresCount);

    /// <summary>
    /// Increments the publish counter, allowing batch operations to record multiple successes.
    /// </summary>
    private static void IncrementPublished(int successesCount = 1) => _publishedCounter.Add(successesCount);

    /// <summary>
    /// Logs comprehensive batch processing results including performance metrics and success rates.
    /// </summary>
    /// <param name="topicName">The topic name targeted by this batch operation.</param>
    /// <param name="context">The batch context containing processing results and statistics.</param>
    /// <param name="batchStopwatch">The stopwatch containing the total batch processing duration.</param>
    private void LogBatchResults(string topicName, BatchProducingContext context, Stopwatch batchStopwatch)
    {
        var successRate = context.ValidMessages.Count == 0
            ? 0
            : (double)context.PublishedMessages.Count / context.ValidMessages.Count;

        _logger.LogInformation(
            "// KafkaProducer // ProduceAsync // Topic={Topic} TotalValid={TotalValid} Published={PublishedCount} NotPublished={UnpublishedMessages} SuccessRate={Rate:P2} DurationMs={Duration:F0}",
            topicName,
            context.ValidMessages.Count,
            context.PublishedMessages.Count,
            context.UnpublishedMessages.Count,
            successRate,
            batchStopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Processes delivery task results from Kafka produce operations and categorizes messages into published and unpublished groups.
    /// </summary>
    /// <param name="context">The batch processing context to update with delivery results.</param>
    /// <param name="deliveryTasks">Collection of completed delivery tasks returned by Kafka <c>ProduceAsync</c> operations.</param>
    /// <returns>
    /// Updated <see cref="BatchProducingContext"/> with messages categorized based on delivery results.
    /// Successfully persisted messages are added to <see cref="BatchProducingContext.PublishedMessages"/>.
    /// Failed, canceled, or non-persisted messages are added to <see cref="BatchProducingContext.UnpublishedMessages"/>.
    /// Failure metrics are automatically incremented for unpublished messages.
    /// </returns>
    private static BatchProducingContext CategorizeDeliveryResults(BatchProducingContext context, List<Task<DeliveryResult<Null, string>>> deliveryTasks)
    {
        var unpublishedMessages = new List<string>();
        var publishedMessages = new List<string>(deliveryTasks.Count);

        foreach (var deliveryTask in deliveryTasks)
        {
            if (deliveryTask.IsCanceled)
            {
                IncrementFailed();

                continue;
            }

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
                    unpublishedMessages.Add(failedPayload);

                    IncrementFailed();
                }

                continue;
            }

            var deliveryTaskResult = deliveryTask.Result;
            var payload = deliveryTaskResult.Message?.Value ?? deliveryTaskResult.Value;

            if (deliveryTaskResult.Status == PersistenceStatus.Persisted)
            {
                publishedMessages.Add(payload);

                IncrementPublished();
            }
            else
            {
                unpublishedMessages.Add(payload);

                IncrementFailed();
            }
        }

        return context with
        {
            PublishedMessages = [.. publishedMessages],
            UnpublishedMessages = [.. unpublishedMessages]
        };
    }

    /// <summary>
    /// Creates deferred execution task factories for each valid message in the batch context.
    /// </summary>
    /// <param name="topicName">The topic name targeted by this batch operation.</param>
    /// <param name="batchContext">The batch context containing valid and invalid messages.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the task factory creation process.</param>
    /// <returns>
    /// An updated batch context with deferred task factories that can be executed to produce messages.
    /// </returns>
    private BatchProducingContext CreateDeferredProduceTaskFactories(string topicName, BatchProducingContext batchContext, CancellationToken cancellationToken)
    {
        if (!batchContext.ValidMessages.Any())
        {
            return batchContext;
        }

        int scheduledMessagesCount = 0;
        var producingTaskFactories = new List<Func<Task<DeliveryResult<Null, string>>>>(batchContext.ValidMessages.Count);

        foreach (var validMessage in batchContext.ValidMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "// KafkaProducer // ProduceAsync // CreateProduceTaskFactories // Cancellation Requested. Topic={Topic} Scheduled={Scheduled} Unscheduled={Unscheduled}",
                    topicName,
                    scheduledMessagesCount,
                    batchContext.ValidMessages.Count - scheduledMessagesCount);

                break;
            }

            // Capture the message in a local variable to avoid closure issues
            var messagePayload = validMessage;

            producingTaskFactories.Add(() => _producer.ProduceAsync(topicName, new Message<Null, string> { Value = messagePayload }, cancellationToken));

            scheduledMessagesCount++;
        }

        return batchContext with
        {
            DeferredProduceTaskFactories = [.. producingTaskFactories]
        };
    }
}
