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
    private readonly KafkaSettings _settings;
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
    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
        : base(settings.Value)
    {
        _logger = logger;
        _settings = settings.Value;

        var config = BuildProducerConfig();

        _producer = BuildProducer(config);

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

        var publishStartTime = Stopwatch.StartNew();

        try
        {
            var result = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = message });

            if (result.Status != PersistenceStatus.Persisted)
            {
                _logger.LogError(
                    "// KafkaProducer // ProduceAsync // Message not persisted. Status={Status} Partition={Partition}",
                    result.Status,
                    result.Partition);

                IncrementFailed();

                return false;
            }

            IncrementPublished();

            return true;
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(
                ex,
                "// KafkaProducer // ProduceAsync // ProduceException Code={Code} Reason={Reason} MessageLength={Length}",
                ex.Error.Code,
                ex.Error.Reason,
                message.Length);

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
            publishStartTime.Stop();

            _singleLatencyMs.Record(publishStartTime.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> ProduceAsync(string topic, IEnumerable<string> messages, CancellationToken cancellationToken = default)
    {
        var batchContext = InitializeBatchContext(topic, messages);

        if (!batchContext.IsValid)
        {
            return batchContext.UnpublishedMessages;
        }

        var batchTiming = Stopwatch.StartNew();

        try
        {
            var (taskFactories, updatedContext) = CreateProduceTaskFactories(batchContext, cancellationToken);

            batchContext = updatedContext;

            if (taskFactories.Count == 0)
            {
                return FinalizeBatch(batchContext, batchTiming);
            }

            var (deliveryTasks, finalContext) = await ExecuteDeliveryTasksAsync(batchContext, cancellationToken);
            
            batchContext = finalContext;

            bool wasCancelled = cancellationToken.IsCancellationRequested && batchContext.ScheduledCount < batchContext.ValidMessages.Count;

            batchContext = ProcessDeliveryResults(deliveryTasks, batchContext, wasCancelled);

            return FinalizeBatch(batchContext, batchTiming);
        }
        catch (Exception ex)
        {
            return HandleBatchException(ex, batchContext, batchTiming);
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
            _logger.LogError(ex, "// KafkaProducer // EnsureTopicsExist // Metadata fetch failed.");
            throw;
        }

        var existingTopics = new HashSet<string>(metadata.Topics.Select(e => e.Topic), StringComparer.OrdinalIgnoreCase);

        foreach (string topic in _settings.Admin.TopicList)
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
                if (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    _logger.LogInformation("// KafkaProducer // EnsureTopicsExist // Topic '{Topic}' already exists.", topic);

                    continue;
                }

                _logger.LogError(ex, "// KafkaProducer // EnsureTopicsExist // Failed to create topic '{Topic}'", topic);
                throw;
            }
        }
    }

    /// <summary>
    /// Validates that the provided topic name is non-null and non-whitespace.
    /// </summary>
    private bool ValidateTopic(string topic)
    {
        if (!string.IsNullOrWhiteSpace(topic))
        {
            return true;
        }

        _logger.LogError("// KafkaProducer // Topic name is null, empty, or whitespace");

        IncrementFailed();

        return false;
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
    /// Builds and configures the producer settings used by the Kafka producer instance.
    /// </summary>
    private ProducerConfig BuildProducerConfig()
    {
        return new(ProducerSettings)
        {
            LingerMs = 100,
            Acks = Acks.All,
            MaxInFlight = 5,
            RetryBackoffMs = 1000,
            BatchSize = 256 * 1024,
            BatchNumMessages = 1000,
            EnableIdempotence = true,
            MessageSendMaxRetries = 5,
            EnableBackgroundPoll = true,
            SocketKeepaliveEnable = true,
            EnableDeliveryReports = true,
            StatisticsIntervalMs = 30000,
            DeliveryReportFields = "status",
            CompressionType = CompressionType.Zstd,
            ClientId = $"{_settings.Consumer.GroupId}-{GetType().Name.ToLower()}"
        };
    }

    /// <summary>
    /// Creates the Kafka producer instance and attaches error and statistics handlers.
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
    private void LogBatchResults(BatchContext context, Stopwatch batchStopwatch)
    {
        var successRate = context.ValidMessages.Count == 0
            ? 0
            : (double)context.SuccessCount / context.ValidMessages.Count;

        _logger.LogInformation(
            "// KafkaProducer // ProduceAsync // Topic={Topic} TotalValid={TotalValid} Success={Success} NotPublished={NotPublished} SuccessRate={Rate:P2} DurationMs={Duration:F0}",
            context.Topic,
            context.ValidMessages.Count,
            context.SuccessCount,
            context.UnpublishedMessages.Count,
            successRate,
            batchStopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Increments the failure counter, allowing batch operations to record multiple failures.
    /// </summary>
    private static void IncrementFailed(int count = 1) => _failedCounter.Add(count);

    /// <summary>
    /// Finalizes the batch processing by updating metrics, recording timing data, and logging results.
    /// </summary>
    /// <param name="context">The batch context containing processing results.</param>
    /// <param name="batchStopwatch">The stopwatch used to measure batch processing duration.</param>
    /// <returns>
    /// The collection of unpublished messages from the batch context.
    /// </returns>
    private List<string> FinalizeBatch(BatchContext context, Stopwatch batchStopwatch)
    {
        if (context.SuccessCount > 0)
        {
            _publishedCounter.Add(context.SuccessCount);
        }

        batchStopwatch.Stop();
        _batchLatencyMs.Record(batchStopwatch.Elapsed.TotalMilliseconds);

        LogBatchResults(context, batchStopwatch);

        return context.UnpublishedMessages;
    }

    /// <summary>
    /// Increments the publish counter, allowing batch operations to record multiple successes.
    /// </summary>
    private static void IncrementPublished(int count = 1) => _publishedCounter.Add(count);

    /// <summary>
    /// Initializes and validates the batch processing context, including topic validation and message categorization.
    /// </summary>
    /// <param name="topic">The name of the Kafka topic to publish messages to.</param>
    /// <param name="messages">The collection of messages to be processed and published.</param>
    /// <returns>
    /// A <see cref="BatchContext"/> object containing the categorized messages and validation status.
    /// </returns>
    private BatchContext InitializeBatchContext(string topic, IEnumerable<string> messages)
    {
        var list = messages as List<string> ?? [.. messages];

        if (!ValidateTopic(topic))
        {
            IncrementFailed(list.Count);

            return new BatchContext
            {
                Topic = topic,
                IsValid = false,
                SuccessCount = 0,
                ValidMessages = [],
                ScheduledCount = 0,
                InvalidMessages = [],
                UnpublishedMessages = [.. list]
            };
        }

        var invalid = new List<string>();
        var valid = new List<string>(list.Count);

        foreach (var m in list)
        {
            if (string.IsNullOrWhiteSpace(m))
            {
                invalid.Add(m);
            }
            else
            {
                valid.Add(m);
            }
        }

        if (invalid.Count > 0)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // {Count} invalid messages", invalid.Count);
            IncrementFailed(invalid.Count);
        }

        bool isValid = valid.Count > 0;
        if (!isValid)
        {
            _logger.LogError("// KafkaProducer // ProduceAsync // No valid messages to publish");
        }

        return new BatchContext
        {
            Topic = topic,
            SuccessCount = 0,
            IsValid = isValid,
            ScheduledCount = 0,
            ValidMessages = valid,
            InvalidMessages = invalid,
            UnpublishedMessages = [.. invalid]
        };
    }

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
    private List<string> HandleBatchException(Exception ex, BatchContext context, Stopwatch batchStopwatch)
    {
        _logger.LogError(ex, "// KafkaProducer // ProduceAsync // Unexpected failure");

        var unprocessedMessages = context.ValidMessages
            .Take(context.ScheduledCount)
            .Where(m => !context.UnpublishedMessages.Contains(m));

        context.UnpublishedMessages.AddRange(unprocessedMessages);
        IncrementFailed(context.ScheduledCount);

        batchStopwatch.Stop();
        _batchLatencyMs.Record(batchStopwatch.Elapsed.TotalMilliseconds);

        return context.UnpublishedMessages;
    }

    /// <summary>
    /// Processes the delivery task results and updates the batch context with success and failure counts.
    /// </summary>
    /// <param name="deliveryTasks">The result object containing delivery results or task information.</param>
    /// <param name="context">The batch context to update with processing results.</param>
    /// <param name="wasCancelled">A token that can be used to cancel the scheduling operation.</param>
    /// <returns>An updated BatchContext with the processing results.</returns>
    private static BatchContext ProcessDeliveryResults(List<Task<DeliveryResult<Null, string>>> deliveryTasks, BatchContext context, bool wasCancelled)
    {
        if (wasCancelled)
        {
            return ProcessCancelledDeliveryTasks(deliveryTasks, context);
        }

        // All tasks awaited in ExecuteDeliveryTasksAsync before calling this
        var results = new DeliveryResult<Null, string>[deliveryTasks.Count];
        for (int i = 0; i < deliveryTasks.Count; i++)
        {
            // Task should be completed; if faulted or cancelled treat as failure
            var t = deliveryTasks[i];
            if (t.IsCompletedSuccessfully && t.Result.Status == PersistenceStatus.Persisted)
            {
                results[i] = t.Result;
            }
            else
            {
                // Create a synthetic failed delivery result if needed
                results[i] = t.IsCompletedSuccessfully ? t.Result : new DeliveryResult<Null, string>
                {
                    Message = new Message<Null, string> { Value = context.ValidMessages[i] },
                    Status = PersistenceStatus.NotPersisted
                };
            }
        }

        return ProcessCompletedDeliveryResults(results, context);
    }

    /// <summary>
    /// Processes completed delivery results, checking the persistence status of each result.
    /// </summary>
    /// <param name="deliveryResults">The array of delivery results to process.</param>
    /// <param name="context">The batch context to update with results.</param>
    /// <returns>An updated BatchContext with the processing results.</returns>
    private static BatchContext ProcessCompletedDeliveryResults(DeliveryResult<Null, string>[] deliveryResults, BatchContext context)
    {
        int failureCount = 0;
        int successCount = context.SuccessCount;
        var updatedUnpublishedMessages = new List<string>(context.UnpublishedMessages);

        for (int i = 0; i < deliveryResults.Length; i++)
        {
            if (deliveryResults[i].Status == PersistenceStatus.Persisted)
            {
                successCount++;
            }
            else
            {
                updatedUnpublishedMessages.Add(context.ValidMessages[i]);
                failureCount++;
            }
        }

        if (failureCount > 0)
        {
            IncrementFailed(failureCount);
        }

        return context with
        {
            SuccessCount = successCount,
            UnpublishedMessages = updatedUnpublishedMessages
        };
    }

    /// <summary>
    /// Processes delivery tasks that were cancelled during execution, checking individual task completion status.
    /// </summary>
    /// <param name="deliveryTasks">The list of delivery tasks to process.</param>
    /// <param name="context">The batch context to update with results.</param>
    /// <returns>An updated BatchContext with the processing results.</returns>
    private static BatchContext ProcessCancelledDeliveryTasks(List<Task<DeliveryResult<Null, string>>> deliveryTasks, BatchContext context)
    {
        int successCount = context.SuccessCount;
        var updatedUnpublishedMessages = new List<string>(context.UnpublishedMessages);
        int failureCount = 0;

        for (int i = 0; i < deliveryTasks.Count; i++)
        {
            var task = deliveryTasks[i];
            if (task.IsCompletedSuccessfully && task.Result.Status == PersistenceStatus.Persisted)
            {
                successCount++;
            }
            else
            {
                updatedUnpublishedMessages.Add(context.ValidMessages[i]);
                failureCount++;
            }
        }

        if (failureCount > 0)
        {
            IncrementFailed(failureCount);
        }

        return context with
        {
            SuccessCount = successCount,
            UnpublishedMessages = updatedUnpublishedMessages
        };
    }

    /// <summary>
    /// Prepares (but does not start) produce task factories for each valid message in the batch.
    /// </summary>
    /// <param name="context">The batch context containing valid messages and tracking information.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the scheduling operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains a list of delivery tasks for the scheduled messages.
    /// </returns>
    private (List<Task<DeliveryResult<Null, string>>> DeliveryTasks, BatchContext UpdatedContext) CreateProduceTaskFactories(BatchContext context, CancellationToken cancellationToken)
    {
        int scheduled = 0;
        var unpublished = new List<string>();
        var tasks = new List<Task<DeliveryResult<Null, string>>>(context.ValidMessages.Count);

        foreach (var msg in context.ValidMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                unpublished.AddRange(context.ValidMessages.Skip(scheduled));
                _logger.LogWarning(
                    "// KafkaProducer // ProduceAsync // Cancellation. Scheduled={Scheduled} Remaining={Remaining} Topic={Topic}",
                    scheduled,
                    context.ValidMessages.Count - scheduled,
                    context.Topic);
                break;
            }

            tasks.Add(_producer.ProduceAsync(context.Topic, new Message<Null, string> { Value = msg }, cancellationToken));
            scheduled++;
        }

        var updated = context with
        {
            ScheduledCount = scheduled,
            UnpublishedMessages = unpublished
        };

        return (tasks, updated);
    }

    /// <summary>
    /// Executes the prepared produce task factories, awaits their completion,
    /// and captures results, handling cancellation and Kafka-specific exceptions.
    /// </summary>
    /// <param name="context">The batch context for logging and error tracking.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the execution.</param>
    /// <returns>
    /// A tuple:
    /// <list type="bullet">
    /// <item><description><c>Result</c>: A <see cref="DeliveryTaskResult"/> containing either completed <c>DeliveryResults</c> (when not cancelled) or the raw <c>DeliveryTasks</c> (when cancelled).</description></item>
    /// <item><description><c>UpdatedContext</c>: The (possibly) augmented <see cref="BatchContext"/> reflecting unpublished message additions on failure.</description></item>
    /// </list>
    /// </returns>
    private async Task<(List<Task<DeliveryResult<Null, string>>> DeliveryTasks, BatchContext UpdatedContext)> ExecuteDeliveryTasksAsync(BatchContext context, CancellationToken cancellationToken)
    {
        int scheduled = 0;
        var unpublished = new List<string>();
        var tasks = new List<Task<DeliveryResult<Null, string>>>(context.ValidMessages.Count);

        foreach (var msg in context.ValidMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                unpublished.AddRange(context.ValidMessages.Skip(scheduled));
                break;
            }

            var tcs = new TaskCompletionSource<DeliveryResult<Null, string>>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                _producer.Produce(context.Topic, new Message<Null, string> { Value = msg }, dr =>
                {
                    if (dr.Error.IsError)
                    {
                        tcs.TrySetResult(dr); // still allow processing logic to handle failure
                    }
                    else
                    {
                        tcs.TrySetResult(dr);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// KafkaProducer // Produce // Immediate failure scheduling message.");
                unpublished.Add(msg);
                IncrementFailed();
                continue;
            }

            tasks.Add(tcs.Task);
            scheduled++;
        }

        var updated = context with
        {
            ScheduledCount = scheduled,
            UnpublishedMessages = unpublished
        };

        return (tasks, updated);
    }
}
