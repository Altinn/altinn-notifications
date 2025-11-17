using System.Collections.Concurrent;

using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Abstract base class for Kafka consumers, providing a framework for consuming messages from a specified topic.
/// </summary>
public abstract class KafkaConsumerBase<T> : BackgroundService
    where T : class
{
    private readonly string _topicName;
    private readonly ILogger<T> _logger;
    private volatile bool _isShutdownInitiated;
    private readonly IConsumer<string, string> _consumer;

    private const int _maxMessagesCountInBatch = 200;
    private const int _messagesBatchPollTimeoutInMs = 100;
    private const int _maxConcurrentProcessingTasks = 100;

    private readonly SemaphoreSlim _processingConcurrencySemaphore;
    private readonly ConcurrentDictionary<Guid, Task> _inFlightTasks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerBase{T}"/> class.
    /// </summary>
    protected KafkaConsumerBase(
        IOptions<KafkaSettings> settings,
        ILogger<T> logger,
        string topicName)
    {
        _logger = logger;
        _topicName = topicName;

        var config = new SharedClientConfig(settings.Value);

        var consumerConfig = new ConsumerConfig(config.ConsumerSettings)
        {
            FetchWaitMaxMs = 20,
            QueuedMinMessages = 1000,
            SessionTimeoutMs = 30000,
            EnableAutoCommit = false,
            FetchMinBytes = 64 * 1024,
            HeartbeatIntervalMs = 5000,
            MaxPollIntervalMs = 300000,
            EnableAutoOffsetStore = false,
            QueuedMaxMessagesKbytes = 32768,
            MaxPartitionFetchBytes = 4 * 1024 * 1024,
            SocketReceiveBufferBytes = 2 * 1024 * 1024,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = $"{settings.Value.Consumer.GroupId}-{GetType().Name.ToLower()}"
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                {
                    _logger.LogCritical("FATAL Kafka error. Code={ErrorCode}. Reason={Reason}", e.Code, e.Reason);
                }
                else if (e.IsError)
                {
                    _logger.LogError("Kafka error. Code={ErrorCode}. Reason={Reason}", e.Code, e.Reason);
                }
                else
                {
                    _logger.LogWarning("Kafka warning. Code={ErrorCode}. Reason={Reason}", e.Code, e.Reason);
                }
            })
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation("// {Class} // Partitions assigned: {Partitions}", GetType().Name, string.Join(',', partitions.Select(p => p.Partition.Value)));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogInformation("// {Class} // Partitions revoked: {Partitions}", GetType().Name, string.Join(',', partitions.Select(p => p.Partition.Value)));
            })
            .Build();

        _processingConcurrencySemaphore = new SemaphoreSlim(_maxConcurrentProcessingTasks, _maxConcurrentProcessingTasks);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_topicName);

        _logger.LogInformation("// {Class} // Subscribed to topic {Topic}", GetType().Name, _topicName);

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _isShutdownInitiated = true;

        var processingTasks = _inFlightTasks.Values.ToArray();

        _logger.LogInformation("// {Class} // Shutdown initiated. In-flight tasks: {Count}", GetType().Name, processingTasks.Length);

        if (processingTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(processingTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// {Class} // Error awaiting tasks during shutdown", GetType().Name);
            }
        }

        _consumer.Unsubscribe();

        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override abstract Task ExecuteAsync(CancellationToken stoppingToken);

    /// <summary>
    /// Consumes messages from the Kafka topic in batches, ensuring efficient processing with bulk commits.
    /// </summary>
    /// <param name="processMessageFunc">Function to process a single message.</param>
    /// <param name="retryMessageFunc">Function to retry a message if processing fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task ConsumeMessage(Func<string, Task> processMessageFunc, Func<string, Task> retryMessageFunc, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_isShutdownInitiated)
        {
            try
            {
                var messageBatch = FetchMessageBatch(_maxMessagesCountInBatch, _messagesBatchPollTimeoutInMs, cancellationToken);

                if (messageBatch.Length == 0)
                {
                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                _logger.LogInformation(
                    "// {Class} // Polled {BatchSize} messages. Offsets {StartOffset}..{EndOffset}. In-flight tasks: {InFlight}",
                    GetType().Name,
                    messageBatch.Length,
                    messageBatch[0].Offset,
                    messageBatch[^1].Offset,
                    _inFlightTasks.Count);

                var processingTasks = new List<Task>();
                var processingStartTime = DateTime.UtcNow;
                var successfulOffsets = new ConcurrentBag<TopicPartitionOffset>();

                foreach (var message in messageBatch)
                {
                    await _processingConcurrencySemaphore.WaitAsync(cancellationToken);

                    var processingTaskId = Guid.NewGuid();

                    _logger.LogInformation("// {Class} // Start processing message at offset {Offset}", GetType().Name, message.Offset);

                    var processingTask = Task.Run(
                        async () =>
                        {
                            try
                            {
                                await processMessageFunc(message.Message.Value);
                                successfulOffsets.Add(new TopicPartitionOffset(message.TopicPartition, message.Offset + 1));
                                _logger.LogInformation("// {Class} // Successfully processed message at offset {Offset}", GetType().Name, message.Offset);
                            }
                            catch (OperationCanceledException)
                            {
                                // Shutdown scenario - don't retry
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "// {Class} // Error processing message at offset {Offset}, attempting retry", GetType().Name, message.Offset);

                                try
                                {
                                    await retryMessageFunc(message.Message.Value);

                                    successfulOffsets.Add(new TopicPartitionOffset(message.TopicPartition, message.Offset + 1));

                                    _logger.LogInformation("// {Class} // Retry succeeded for message at offset {Offset}", GetType().Name, message.Offset);
                                }
                                catch (Exception retryEx)
                                {
                                    _logger.LogError(retryEx, "// {Class} // Retry failed for message at offset {Offset}", GetType().Name, message.Offset);
                                }
                            }
                            finally
                            {
                                _processingConcurrencySemaphore.Release();

                                _inFlightTasks.TryRemove(processingTaskId, out _);

                                _logger.LogInformation("// {Class} // Released semaphore for message at offset {Offset}. In-flight tasks: {InFlight}", GetType().Name, message.Offset, _inFlightTasks.Count);
                            }
                        },
                        cancellationToken);

                    _inFlightTasks[processingTaskId] = processingTask;

                    processingTasks.Add(processingTask);
                }

                await Task.WhenAll(processingTasks);

                if (!successfulOffsets.IsEmpty)
                {
                    _logger.LogInformation("// {Class} // Committing {Count} offsets to Kafka", GetType().Name, successfulOffsets.Count);

                    SafeCommit(successfulOffsets);
                }
                else
                {
                    _logger.LogWarning("// {Class} // No messages successfully processed in batch of {BatchSize}", GetType().Name, messageBatch.Length);
                }

                _logger.LogInformation(
                    "// KafkaConsumerBase // ConsumeMessage // Batch consuming completed for topic {Topic}. Processed batch of {BatchSize} messages in {Duration:F0}ms, committed {CommittedCount} offsets",
                    _topicName,
                    messageBatch.Length,
                    (DateTime.UtcNow - processingStartTime).TotalMilliseconds,
                    successfulOffsets.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// {Class} // Unexpected error in batch processing loop", GetType().Name);
            }
        }
    }

    /// <summary>
    /// Commits a batch of processed offsets to Kafka, normalizing duplicates per partition (keeping the highest offset).
    /// </summary>
    /// <param name="offsets">The offsets to commit.</param>
    private void SafeCommit(IEnumerable<TopicPartitionOffset> offsets)
    {
        if (_isShutdownInitiated || offsets is null)
        {
            return;
        }

        var normalized = offsets
            .GroupBy(o => o.TopicPartition)
            .Select(g =>
            {
                var max = g.Select(x => x.Offset.Value).Max();
                return new TopicPartitionOffset(g.Key, new Offset(max));
            })
            .ToList();

        if (normalized.Count == 0)
        {
            return;
        }

        _logger.LogInformation("// {Class} // Committing normalized offsets per partition: {Offsets}", GetType().Name, string.Join(',', normalized.Select(o => $"{o.Topic}:{o.Partition}-{o.Offset}")));

        try
        {
            _consumer.Commit(normalized);

            _logger.LogInformation("// {Class} // Successfully committed {Count} offsets", GetType().Name, normalized.Count);
        }
        catch (KafkaException ex) when (ex.Error.Code is ErrorCode.RebalanceInProgress or ErrorCode.IllegalGeneration)
        {
            _logger.LogWarning(ex, "// {Class} // Bulk commit skipped due to transient state: {Reason}", GetType().Name, ex.Error.Reason);
        }
        catch (KafkaException ex)
        {
            _logger.LogError(ex, "// {Class} // Bulk commit failed unexpectedly", GetType().Name);
        }
    }

    /// <summary>
    /// Polls messages from the underlying Kafka consumer until one of the stopping conditions is met.
    /// </summary>
    /// <param name="maxBatchSize">The maximum number of messages to return.</param>
    /// <param name="timeoutMs">The total maximum time (in milliseconds) spent polling for this batch.</param>
    /// <param name="cancellationToken">Token observed for cooperative cancellation.</param>
    /// <returns>An array (possibly empty) of consecutively polled <see cref="ConsumeResult{TKey,TValue}"/> instances.</returns>
    private ConsumeResult<string, string>[] FetchMessageBatch(int maxBatchSize, int timeoutMs, CancellationToken cancellationToken)
    {
        if (maxBatchSize <= 0 || timeoutMs <= 0 || cancellationToken.IsCancellationRequested)
        {
            return [];
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var polledMessages = new List<ConsumeResult<string, string>>(maxBatchSize);

        while (polledMessages.Count < maxBatchSize && !cancellationToken.IsCancellationRequested)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                var result = _consumer.Consume(remaining);
                if (result is null)
                {
                    break;
                }

                polledMessages.Add(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "// {Class} // Consume exception during batch polling", GetType().Name);
                break;
            }
        }

        _logger.LogInformation("// {Class} // Fetched {Count} messages from Kafka in batch", GetType().Name, polledMessages.Count);

        return [.. polledMessages];
    }
}
