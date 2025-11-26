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
/// Implementation of a Kafka producer.
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

            _logger.LogError(
                "// KafkaProducer // ProduceAsync // Message not persisted. Status={Status} Partition={Partition}",
                produceResult.Status,
                produceResult.Partition);

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
        var BatchProducingContext = InitializeBatchProducingContext(topic, messages);
        if (!BatchProducingContext.HasValidMessages)
        {
            return messages;
        }

        var batchProcessingStopwatch = Stopwatch.StartNew();

        try
        {
            BatchProducingContext = CreateProduceTaskFactories(BatchProducingContext, cancellationToken);
            if (BatchProducingContext.TaskFactories.Count == 0)
            {
                batchProcessingStopwatch.Stop();

                LogBatchResults(BatchProducingContext, batchProcessingStopwatch);

                return messages;
            }

            var publishTasks = new List<Task<DeliveryResult<Null, string>>>(BatchProducingContext.TaskFactories.Count);
            try
            {
                foreach (var factory in BatchProducingContext.TaskFactories)
                {
                    publishTasks.Add(factory());
                }

                await Task.WhenAll(publishTasks).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Exception during Task.WhenAll");
            }

            BatchProducingContext = ProcessDeliveryResults([.. publishTasks], BatchProducingContext);

            if (BatchProducingContext.PublishedCount > 0)
            {
                _publishedCounter.Add(BatchProducingContext.PublishedCount);
            }

            batchProcessingStopwatch.Stop();

            _batchLatencyMs.Record(batchProcessingStopwatch.Elapsed.TotalMilliseconds);

            LogBatchResults(BatchProducingContext, batchProcessingStopwatch);

            return [.. BatchProducingContext.InvalidMessages, .. BatchProducingContext.UnpublishedMessages];
        }
        catch (Exception ex)
        {
            return HandleBatchException(ex, BatchProducingContext, batchProcessingStopwatch);
        }
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
            LogMetadataFetchFailed(ex);

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

                LogTopicCreationFailed(ex, topic);

                throw;
            }
        }
    }

    /// <summary>
    /// Validates that the provided topic name is non-null and non-whitespace.
    /// </summary>
    private bool ValidateTopic(string topic)
    {
        if (!string.IsNullOrWhiteSpace(topic) && _kafkaSettings.Admin.TopicList.Contains(topic))
        {
            return true;
        }

        _logger.LogError("// KafkaProducer // Topic name is null, empty, or whitespace");

        IncrementFailed();

        return false;
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
            DeliveryReportFields = "status",
            CompressionType = CompressionType.Zstd
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

        _logger.LogError("// KafkaProducer // Message is null, empty, or whitespace");

        IncrementFailed();

        return false;
    }

    /// <summary>
    /// Logs the error that occurred while fetching Kafka metadata in EnsureTopicsExist.
    /// </summary>
    /// <param name="exceptionOccurred">The exception occurred.</param>
    private void LogMetadataFetchFailed(Exception exceptionOccurred)
    {
        _logger.LogError(exceptionOccurred, "// KafkaProducer // EnsureTopicsExist // Metadata fetch failed.");
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
    /// Logs comprehensive batch processing results including performance metrics and success rates.
    /// </summary>
    /// <param name="context">The batch context containing processing results and statistics.</param>
    /// <param name="batchStopwatch">The stopwatch containing the total batch processing duration.</param>
    private void LogBatchResults(BatchProducingContext context, Stopwatch batchStopwatch)
    {
        var successRate = context.ValidMessages.Count == 0
            ? 0
            : (double)context.PublishedCount / context.ValidMessages.Count;

        _logger.LogInformation(
            "// KafkaProducer // ProduceAsync // Topic={Topic} TotalValid={TotalValid} Published={PublishedCount} NotPublished={UnpublishedMessages} SuccessRate={Rate:P2} DurationMs={Duration:F0}",
            context.Topic,
            context.ValidMessages.Count,
            context.PublishedCount,
            context.UnpublishedMessages.Count,
            successRate,
            batchStopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Logs the error that occurred while creating missing topics in EnsureTopicsExist.
    /// </summary>
    /// <param name="exceptionOccurred">The exception occurred.</param>
    /// <param name="topicName">The name of the topic.</param>
    private void LogTopicCreationFailed(Exception exceptionOccurred, string topicName)
    {
        _logger.LogError(exceptionOccurred, "// KafkaProducer // EnsureTopicsExist // Failed to create topic '{Topic}'", topicName);
    }

    /// <summary>
    /// Validates topic and messages, then creates a batch context with categorized valid/invalid messages.
    /// </summary>
    /// <param name="topic">The Kafka topic name to validate and use for publishing.</param>
    /// <param name="messages">The message collection to validate and categorize.</param>
    /// <returns>
    /// A <see cref="BatchProducingContext"/> with validation results and categorized messages.
    /// </returns>
    private BatchProducingContext InitializeBatchProducingContext(string topic, IImmutableList<string> messages)
    {
        if (!ValidateTopic(topic) || messages.Count == 0)
        {
            return new BatchProducingContext();
        }

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
            Topic = topic,
            ValidMessages = [.. validMessages],
            InvalidMessages = [.. invalidMessages],
            UnpublishedMessages = [.. validMessages],
            HasValidMessages = containSingleValidMessage
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
    /// Handles exceptions that occur during batch processing, ensuring proper cleanup and error reporting.
    /// </summary>
    /// <param name="ex">The exception that occurred during batch processing.</param>
    /// <param name="context">The batch context containing processing state information.</param>
    /// <param name="batchStopwatch">The stopwatch used to measure batch processing duration.</param>
    /// <returns>
    /// The collection of unpublished messages, including both originally unpublished messages 
    /// and any messages that were scheduled but not processed due to the exception.
    /// </returns>
    private List<string> HandleBatchException(Exception ex, BatchProducingContext context, Stopwatch batchStopwatch)
    {
        _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Unexpected failure");

        var unprocessedMessages = context.ValidMessages
            .Take(context.ScheduledCount)
            .Where(e => !context.UnpublishedMessages.Contains(e));

        context.UnpublishedMessages.AddRange(unprocessedMessages);
        IncrementFailed(unprocessedMessages.Count());

        batchStopwatch.Stop();
        _batchLatencyMs.Record(batchStopwatch.Elapsed.TotalMilliseconds);

        return [.. context.UnpublishedMessages];
    }

    /// <summary>
    /// Creates deferred execution task factories for each valid message.
    /// </summary>
    /// <param name="context">The batch context containing valid messages.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the task factory creation process.</param>
    /// <returns>
    /// <returns>An updated batch context with task factories and scheduling information.</returns>
    private BatchProducingContext CreateProduceTaskFactories(BatchProducingContext context, CancellationToken cancellationToken)
    {
        if (!context.HasValidMessages || !context.ValidMessages.Any())
        {
            return context;
        }

        int scheduledMessagesCount = 0;
        List<string>? unscheduledMessages = null;
        var producingTaskFactories = new List<Func<Task<DeliveryResult<Null, string>>>>(context.ValidMessages.Count);

        foreach (var validMessage in context.ValidMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var unscheduledMessagesCount = context.ValidMessages.Count - scheduledMessagesCount;
                unscheduledMessages = new List<string>(unscheduledMessagesCount);
                unscheduledMessages.AddRange(context.ValidMessages.Skip(scheduledMessagesCount));

                _logger.LogWarning(
                    "// KafkaProducer // ProduceAsync // CreateProduceTaskFactories // Cancellation Requested. Topic={Topic} Scheduled={Scheduled} Unscheduled={Unscheduled}",
                    context.Topic,
                    scheduledMessagesCount,
                    unscheduledMessagesCount);

                break;
            }

            // Capture the message in a local variable to avoid closure issues
            var messagePayload = validMessage;

            producingTaskFactories.Add(() => _producer.ProduceAsync(context.Topic, new Message<Null, string> { Value = messagePayload }, cancellationToken));

            scheduledMessagesCount++;
        }

        var updatedBatchProducingContext = context with
        {
            ScheduledCount = scheduledMessagesCount,
            TaskFactories = [.. producingTaskFactories]
        };

        return updatedBatchProducingContext;
    }

    /// <summary>
    /// Processes the delivery task results and updates the batch context with success and failure counts.
    /// </summary>
    /// <param name="deliveryTasks">The result object containing delivery results or task information.</param>
    /// <param name="context">The batch context to update with processing results.</param>
    /// <returns>An updated BatchProducingContext with the processing results.</returns>
    private static BatchProducingContext ProcessDeliveryResults(List<Task<DeliveryResult<Null, string>>> deliveryTasks, BatchProducingContext context)
    {
        int failureCount = 0;
        int successCount = 0;
        var unpublishedMessages = new List<string>();

        for (int i = 0; i < deliveryTasks.Count; i++)
        {
            var deliveryTask = deliveryTasks[i];
            if (deliveryTask.IsCompletedSuccessfully && deliveryTask.Result.Status == PersistenceStatus.Persisted)
            {
                successCount++;
            }
            else
            {
                failureCount++;
                unpublishedMessages.Add(context.ValidMessages[i]);
            }
        }

        if (failureCount > 0)
        {
            IncrementFailed(failureCount);
        }

        return context with
        {
            PublishedCount = successCount,
            UnpublishedMessages = [.. unpublishedMessages]
        };
    }
}
