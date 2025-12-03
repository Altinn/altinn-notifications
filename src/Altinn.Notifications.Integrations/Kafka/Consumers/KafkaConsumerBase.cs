using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;

using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Abstract base class for Kafka consumers, providing a framework for consuming messages from a specified topic.
/// Maximizes throughput with bounded parallelism while enforcing: do not start more messages after the first failure
/// (including failed retry). Already-started messages finish; contiguous successes are committed.
/// </summary>
public abstract class KafkaConsumerBase : BackgroundService
{
    private readonly string _topicName;
    private volatile bool _isShutdownInitiated;
    private readonly ILogger _logger;
    private readonly IConsumer<string, string> _consumer;

    private readonly int _maxMessagesPerBatch = 50;
    private readonly int _messagesBatchPollTimeoutMs = 100;
    private readonly int _maxConcurrentProcessingTasks = 50;

    private readonly SemaphoreSlim _processingConcurrencySemaphore;
    private readonly ConcurrentDictionary<Guid, Task> _inFlightTasks = new();

    private static readonly Meter _meter = new("Altinn.Notifications.KafkaConsumer", "1.0.0");
    private static readonly Counter<int> _failedCounter = _meter.CreateCounter<int>("kafka.consumer.failed");
    private static readonly Counter<int> _retriedCounter = _meter.CreateCounter<int>("kafka.consumer.retried");
    private static readonly Counter<int> _consumedCounter = _meter.CreateCounter<int>("kafka.consumer.consumed");
    private static readonly Counter<int> _committedCounter = _meter.CreateCounter<int>("kafka.consumer.committed");
    private static readonly Counter<int> _processedCounter = _meter.CreateCounter<int>("kafka.consumer.processed");
    private static readonly Histogram<double> _batchLatencyMs = _meter.CreateHistogram<double>("kafka.consumer.batch.latency.ms");

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerBase"/> class.
    /// </summary>
    protected KafkaConsumerBase(string topicName, IOptions<KafkaSettings> settings, ILogger logger)
    {
        _logger = logger;
        _topicName = topicName;

        var configuration = BuildConfiguration(settings);
        _consumer = BuildConsumer(configuration);

        _processingConcurrencySemaphore = new SemaphoreSlim(_maxConcurrentProcessingTasks, _maxConcurrentProcessingTasks);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        try
        {
            _consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// KafkaConsumer Dispose // Close failed");
        }
        finally
        {
            _consumer.Dispose();
            _processingConcurrencySemaphore.Dispose();
            base.Dispose();
        }
    }

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_topicName);

        _logger.LogInformation("// {Class} // Subscribed to topic {Topic}", GetType().Name, ComputeTopicFingerprint(_topicName));

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _isShutdownInitiated = true;

        _consumer.Unsubscribe();

        var processingTasks = _inFlightTasks.Values.ToArray();

        _logger.LogInformation("// {Class} // Shutdown initiated. In-flight tasks: {Count}", GetType().Name, processingTasks.Length);

        foreach (var processingTask in processingTasks)
        {
            try
            {
                await processingTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (TimeoutException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override abstract Task ExecuteAsync(CancellationToken stoppingToken);

    /// <summary>
    /// Consumes messages from the Kafka topic in batches, launching up to a bounded number of concurrent processors.
    /// </summary>
    /// <param name="processMessageFunc">A function that processes a single message value.</param>
    /// <param name="retryMessageFunc">A function that retries processing a single message value when the initial processing fails.</param>
    /// <param name="cancellationToken">A cancellation token used to observe shutdown requests and coordinate graceful termination of processing tasks.</param>
    protected async Task ConsumeMessage(Func<string, Task> processMessageFunc, Func<string, Task> retryMessageFunc, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_isShutdownInitiated)
        {
            var batchStopwatch = Stopwatch.StartNew();

            try
            {
                var messageBatch = FetchMessageBatch(_maxMessagesPerBatch, _messagesBatchPollTimeoutMs, cancellationToken);
                if (messageBatch.Length == 0)
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                _consumedCounter.Add(messageBatch.Length, KeyValuePair.Create<string, object?>("topic", _topicName));

                _logger.LogInformation(
                    "// {Class} // Polled {BatchSize} messages. Offsets {StartOffset}..{EndOffset}. In-flight tasks: {InFlight}",
                    GetType().Name,
                    messageBatch.Length,
                    messageBatch[0].Offset,
                    messageBatch[^1].Offset,
                    _inFlightTasks.Count);

                var batchProcessingResult = await LaunchBatchProcessing(messageBatch, processMessageFunc, retryMessageFunc, cancellationToken);

                await AwaitLaunchedTasksAndCommit(batchProcessingResult.LaunchedMessages, batchProcessingResult.SuccessfulNextOffsets);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// {Class} // Unexpected error in batch processing loop", GetType().Name);
            }
            finally
            {
                batchStopwatch.Stop();

                _batchLatencyMs.Record(batchStopwatch.Elapsed.TotalMilliseconds, KeyValuePair.Create<string, object?>("topic", _topicName));
            }
        }
    }

    /// <summary>
    /// Computes a deterministic truncated SHA-256 hexadecimal fingerprint for a Kafka topic name.
    /// The fingerprint is intended for log correlation and diagnostics without exposing the raw topic identifier.
    /// </summary>
    /// <param name="topicName">
    /// The original Kafka topic name to fingerprint. If <c>null</c>, empty, or whitespace,
    /// the literal string <c>"EMPTY"</c> is returned.
    /// </param>
    /// <returns>
    /// A 16 character lowercase hexadecimal string representing the first 8 bytes of the SHA-256 hash
    /// of <paramref name="topicName"/>, or <c>"EMPTY"</c> if the input is blank.
    /// </returns>
    private static string ComputeTopicFingerprint(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
        {
            return "EMPTY";
        }

        ReadOnlySpan<byte> topicNameBytes = Encoding.UTF8.GetBytes(topicName);

        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(topicNameBytes, digest);

        // First 8 bytes -> 16 hex chars (truncated fingerprint)
        Span<char> fingerprintBuffer = stackalloc char[16];
        const string hexAlphabet = "0123456789abcdef";

        for (int i = 0; i < 8; i++)
        {
            byte byteValue = digest[i];
            fingerprintBuffer[i * 2] = hexAlphabet[byteValue >> 4];
            fingerprintBuffer[(i * 2) + 1] = hexAlphabet[byteValue & 0x0F];
        }

        return new string(fingerprintBuffer);
    }

    /// <summary>
    /// Builds the Kafka <see cref="ConsumerConfig"/> using the shared client configuration and
    /// applies consumer-specific tuning for batching, throughput and cooperative offset management.
    /// </summary>
    /// <param name="settings">The configuration object used to hold integration settings for Kafka.</param>
    /// <returns>A fully initialized <see cref="ConsumerConfig"/> ready to be used by a <see cref="ConsumerBuilder{TKey, TValue}"/>.</returns>
    private ConsumerConfig BuildConfiguration(IOptions<KafkaSettings> settings)
    {
        var config = new SharedClientConfig(settings.Value);

        var consumerConfig = new ConsumerConfig(config.ConsumerSettings)
        {
            FetchWaitMaxMs = 100,
            QueuedMinMessages = 50,
            SessionTimeoutMs = 30000,
            EnableAutoCommit = false,
            FetchMinBytes = 512 * 1024,
            MaxPollIntervalMs = 300000,
            HeartbeatIntervalMs = 5000,
            EnableAutoOffsetStore = false,
            QueuedMaxMessagesKbytes = 16384,
            MaxPartitionFetchBytes = 4 * 1024 * 1024,
            SocketReceiveBufferBytes = 2 * 1024 * 1024,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = $"{settings.Value.Consumer.GroupId}-{GetType().Name.ToLower()}",
        };

        return consumerConfig;
    }

    /// <summary>
    /// Safely commits normalized offsets to Kafka with built-in resilience against transient consumer group states.
    /// </summary>
    /// <param name="offsets">
    /// A collection the offsets to commit to Kafka.
    /// </param>
    private void SafeCommit(IEnumerable<TopicPartitionOffset> offsets)
    {
        if (offsets is null || _isShutdownInitiated)
        {
            return;
        }

        var normalizedOffsets = offsets
            .GroupBy(e => e.TopicPartition)
            .Select(e =>
            {
                var maxOffset = e.Select(x => x.Offset.Value).Max();
                return new TopicPartitionOffset(e.Key, new Offset(maxOffset));
            })
            .ToList();

        if (normalizedOffsets.Count == 0)
        {
            return;
        }

        try
        {
            _consumer.Commit(normalizedOffsets);

            _committedCounter.Add(normalizedOffsets.Count, KeyValuePair.Create<string, object?>("topic", _topicName));

            _logger.LogInformation(
                "// {Class} // Committed {Count} partition(s). Max next-offsets: {Offsets}",
                GetType().Name,
                normalizedOffsets.Count,
                string.Join(',', normalizedOffsets.Select(o => $"{o.Topic}-{o.Partition}:{o.Offset.Value}")));
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
    /// Creates and configures a Kafka consumer instance with error, statistics, and partition assignment handlers.
    /// </summary>
    /// <param name="consumerConfig">The <see cref="ConsumerConfig"/> used to build the consumer.</param>
    /// <returns>A configured <see cref="IConsumer{TKey, TValue}"/> for consuming messages with string keys and values.</returns>
    private IConsumer<string, string> BuildConsumer(ConsumerConfig consumerConfig)
    {
        return new ConsumerBuilder<string, string>(consumerConfig)
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
            .SetStatisticsHandler((_, json) =>
            {
                _logger.LogDebug("// KafkaConsumerBase // Stats: {StatsJson}", json);
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogInformation("// {Class} // Partitions revoked: {Partitions}", GetType().Name, string.Join(',', partitions.Select(p => p.Partition.Value)));
            })
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation("// {Class} // Partitions assigned: {Partitions}", GetType().Name, string.Join(',', partitions.Select(p => p.Partition.Value)));
            })
            .Build();
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
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("// {Class} // Fetched {Count} messages from Kafka in batch", GetType().Name, polledMessages.Count);

        return [.. polledMessages];
    }

    /// <summary>
    /// Finalizes batch processing by computing contiguous commit candidates from successful message processing results and safely committing them to Kafka.
    /// </summary>
    /// <param name="launchedMessages">
    /// The complete list of messages that were launched for processing in this batch, in the order they were launched.
    /// </param>
    /// <param name="successfulNextOffsets">
    /// A thread-safe collection containing the "next offset" (original offset + 1) for each message that was
    /// successfully processed (including successful retries). These represent the offsets that are safe to commit to Kafka.
    /// </param>
    /// <returns>
    /// A task that completes when all commit operations and logging have finished.
    /// </returns>
    private async Task AwaitLaunchedTasksAndCommit(List<ConsumeResult<string, string>> launchedMessages, ConcurrentBag<TopicPartitionOffset> successfulNextOffsets)
    {
        if (!successfulNextOffsets.IsEmpty)
        {
            var commitCandidates = ComputeContiguousCommitOffsets([.. launchedMessages], successfulNextOffsets);

            if (commitCandidates.Count != 0)
            {
                SafeCommit(commitCandidates);
            }
            else
            {
                _logger.LogWarning("// {Class} // No contiguous offsets eligible for commit in launched set of {Launched}", GetType().Name, launchedMessages.Count);
            }
        }
        else
        {
            _logger.LogWarning("// {Class} // No messages successfully processed in launched set of {Launched}", GetType().Name, launchedMessages.Count);
        }

        await Task.Yield();
    }

    /// <summary>
    /// Computes per-partition commit offsets by determining the largest contiguous
    /// sequence of successfully processed  messages from the earliest offset in each partition within the launched batch.
    /// </summary>
    /// <param name="launchedBatch">
    /// An array of Kafka consume results representing the subset of messages that were actually launched for processing in this batch.
    /// </param>
    /// <param name="successfulOffsets">
    /// A collection of TopicPartitionOffset values representing the "next offset" (original message offset + 1) for 
    /// each message that completed processing successfully, including those that succeeded after retry attempts.
    /// </param>
    /// <returns>
    /// A list of TopicPartitionOffset values that are safe to commit to Kafka, representing the highest contiguous
    /// offset that can be committed for each partition without creating gaps. Returns an empty list if no contiguous
    /// sequences can be established (e.g., if the first message in any partition failed).
    /// </returns>
    private static List<TopicPartitionOffset> ComputeContiguousCommitOffsets(ConsumeResult<string, string>[] launchedBatch, IEnumerable<TopicPartitionOffset> successfulOffsets)
    {
        var commitOffsets = new List<TopicPartitionOffset>();

        // For each partition, get the ordered list of launched offsets.
        var batchByTopicPartition = launchedBatch.GroupBy(e => e.TopicPartition).ToDictionary(e => e.Key, e => e.Select(e => e.Offset.Value).OrderBy(x => x).ToList());

        // Map successes by TopicPartition to a set of next-offset values (since commits use next-offset)
        var successesByTopicPartition = successfulOffsets.GroupBy(e => e.TopicPartition).ToDictionary(e => e.Key, e => new HashSet<long>(e.Select(s => s.Offset.Value)));

        foreach (var kvp in batchByTopicPartition)
        {
            var topicPartition = kvp.Key;
            var orderedOffsets = kvp.Value;

            if (!successesByTopicPartition.TryGetValue(topicPartition, out var successSet) || successSet.Count == 0)
            {
                continue;
            }

            long? lastContiguousNext = null;

            // Find the largest prefix of the ordered launched offsets where each offset's (offset + 1) is in successSet.
            foreach (var offset in orderedOffsets)
            {
                var nextPosition = offset + 1;

                if (successSet.Contains(nextPosition))
                {
                    lastContiguousNext = nextPosition;
                }
                else
                {
                    // Gap encountered — we cannot commit past this point.
                    break;
                }
            }

            if (lastContiguousNext.HasValue)
            {
                commitOffsets.Add(new TopicPartitionOffset(topicPartition, new Offset(lastContiguousNext.Value)));
            }
        }

        return commitOffsets;
    }

    /// <summary>
    /// Launches processing tasks for a batch of messages while honoring bounded concurrency and fail-fast semantics.
    /// </summary>
    /// <param name="messageBatch">The batch of Kafka messages to process, in the order they were consumed from the topic.</param>
    /// <param name="processMessageFunc">A function that processes a single message value.</param>
    /// <param name="retryMessageFunc">A function that retries processing a single message value when the initial processing fails.</param>
    /// <param name="cancellationToken">A cancellation token used to observe shutdown requests and coordinate graceful termination of processing tasks.</param>
    /// <returns>
    /// A <see cref="BatchProcessingResult"/> containing:
    /// - <see cref="BatchProcessingResult.LaunchedMessages"/>: The subset of messages that were launched for processing (may be less than the full batch if fail-fast was triggered).
    /// - <see cref="BatchProcessingResult.SuccessfulNextOffsets"/>: A thread-safe collection of <see cref="TopicPartitionOffset"/> values representing the next offset to commit for successfully processed messages (original offset + 1).
    /// </returns>
    private async Task<BatchProcessingResult> LaunchBatchProcessing(ConsumeResult<string, string>[] messageBatch, Func<string, Task> processMessageFunc, Func<string, Task> retryMessageFunc, CancellationToken cancellationToken)
    {
        var failureDetectedFlag = 0;
        var launchedTasks = new List<Task>(messageBatch.Length);
        var successfulNextOffsets = new ConcurrentBag<TopicPartitionOffset>();
        var launchedMessages = new List<ConsumeResult<string, string>>(messageBatch.Length);

        foreach (var message in messageBatch)
        {
            if (Volatile.Read(ref failureDetectedFlag) == 1 || cancellationToken.IsCancellationRequested || _isShutdownInitiated)
            {
                break;
            }

            // Wait for concurrency slot - this will throw on cancellation
            await _processingConcurrencySemaphore.WaitAsync(cancellationToken);

            launchedMessages.Add(message);
            var processingTaskGuid = Guid.NewGuid();

            var capturedMessage = message;
            var capturedProcessingTaskIdId = processingTaskGuid;

            var processingTask = Task.Run(
                async () =>
                {
                    try
                    {
                        if (Volatile.Read(ref failureDetectedFlag) == 1)
                        {
                            return;
                        }

                        await processMessageFunc(capturedMessage.Message.Value).ConfigureAwait(false);

                        successfulNextOffsets.Add(new TopicPartitionOffset(capturedMessage.TopicPartition, capturedMessage.Offset + 1));

                        _processedCounter.Add(1, KeyValuePair.Create<string, object?>("topic", _topicName));
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Exchange(ref failureDetectedFlag, 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "// {Class} // Error processing message at offset {Offset}, attempting retry", GetType().Name, capturedMessage.Offset);

                        _failedCounter.Add(1, KeyValuePair.Create<string, object?>("topic", _topicName));

                        try
                        {
                            await retryMessageFunc(capturedMessage.Message.Value).ConfigureAwait(false);

                            successfulNextOffsets.Add(new TopicPartitionOffset(capturedMessage.TopicPartition, capturedMessage.Offset + 1));

                            _retriedCounter.Add(1, KeyValuePair.Create<string, object?>("topic", _topicName));
                            _processedCounter.Add(1, KeyValuePair.Create<string, object?>("topic", _topicName));
                        }
                        catch (Exception retryEx)
                        {
                            _logger.LogError(retryEx, "// {Class} // Retry failed for message at offset {Offset}. Halting further launches.", GetType().Name, capturedMessage.Offset);

                            _failedCounter.Add(1, KeyValuePair.Create<string, object?>("topic", _topicName));

                            Interlocked.Exchange(ref failureDetectedFlag, 1);
                        }
                    }
                    finally
                    {
                        try
                        {
                            _processingConcurrencySemaphore.Release();
                        }
                        catch (SemaphoreFullException ex)
                        {
                            _logger.LogCritical(ex, "// {Class} // Semaphore already full when releasing for offset {Offset}", GetType().Name, capturedMessage.Offset);
                        }

                        _inFlightTasks.TryRemove(capturedProcessingTaskIdId, out _);
                    }
                },
                cancellationToken);

            _inFlightTasks[processingTaskGuid] = processingTask;
            launchedTasks.Add(processingTask);
        }

        try
        {
            await Task.WhenAll(launchedTasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "// {Class} // Aggregate completion contained failures", GetType().Name);
        }

        return new BatchProcessingResult(launchedMessages, successfulNextOffsets);
    }
}
