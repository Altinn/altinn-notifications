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

namespace Altinn.Notifications.Integrations.Kafka.Consumers
{
    /// <summary>
    /// Abstract base class for Kafka consumers, offering a robust framework for consuming messages from a specified topic.
    /// </summary>
    public abstract class KafkaConsumerBase : BackgroundService
    {
        private int _batchFailureFlag;
        private int _consumerClosedFlag;
        private int _shutdownStartedFlag;

        private readonly int _maxBatchSize = 100;
        private readonly int _batchPollTimeoutMs = 100;

        private readonly string _topicName;
        private readonly string _topicFingerprint;
        private readonly ILogger _logger;
        private volatile KafkaBatchState? _lastProcessedBatch;
        private readonly IConsumer<string, string> _kafkaConsumer;
        private CancellationTokenSource? _internalCancellationSource;

        private const string _metricsTopicTag = "topic";
        private static readonly Meter _meter = new("Altinn.Notifications.KafkaConsumer", "1.0.0");
        private static readonly Counter<int> _messagesPolledCounter = _meter.CreateCounter<int>("kafka.consumer.polled");
        private static readonly Counter<int> _messagesProcessedCounter = _meter.CreateCounter<int>("kafka.consumer.processed");
        private static readonly Counter<int> _retryFailureCounter = _meter.CreateCounter<int>("kafka.consumer.retried.failed");
        private static readonly Counter<int> _retrySuccessCounter = _meter.CreateCounter<int>("kafka.consumer.retried.succeeded");
        private static readonly Histogram<double> _batchProcessingLatency = _meter.CreateHistogram<double>("kafka.consumer.batch.latency.ms");

        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaConsumerBase"/> class.
        /// </summary>
        protected KafkaConsumerBase(string topicName, IOptions<KafkaSettings> settings, ILogger logger)
        {
            _logger = logger;
            _topicName = topicName;

            var configuration = BuildConfiguration(settings);
            _kafkaConsumer = BuildConsumer(configuration);

            _topicFingerprint = ComputeTopicFingerprint(topicName);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            try
            {
                if (!IsConsumerClosed)
                {
                    SignalConsumerClosure();

                    _kafkaConsumer.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// KafkaConsumer Dispose // Close failed");
            }
            finally
            {
                _kafkaConsumer.Dispose();

                _internalCancellationSource?.Dispose();

                base.Dispose();

                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _internalCancellationSource = new CancellationTokenSource();

            _kafkaConsumer.Subscribe(_topicName);

            _logger.LogInformation("// {Class} // subscribed to topic {Topic}", GetType().Name, _topicFingerprint);

            return base.StartAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_internalCancellationSource != null)
            {
                await _internalCancellationSource.CancelAsync();
            }

            SignalShutdownStarted();

            var lastBatchNormalizedOffsets = _lastProcessedBatch != null
                ? CalculateContiguousCommitOffsets(_lastProcessedBatch.CommitReadyOffsets, _lastProcessedBatch.PolledConsumeResults)
                : [];

            if (lastBatchNormalizedOffsets.Count > 0 && !IsConsumerClosed)
            {
                try
                {
                    _kafkaConsumer.Commit(lastBatchNormalizedOffsets);

                    _logger.LogInformation("// {Class} // Committed last batch safe offsets for processed messages during shutdown", GetType().Name);
                }
                catch (KafkaException ex) when (ex.Error.Code is ErrorCode.RebalanceInProgress or ErrorCode.IllegalGeneration)
                {
                    _logger.LogWarning(ex, "// {Class} // Commit during shutdown skipped due to transient state: {Reason}", GetType().Name, ex.Error.Reason);
                }
                catch (KafkaException ex)
                {
                    _logger.LogError(ex, "// {Class} // Failed to commit last batch safe offsets during shutdown", GetType().Name);
                }
            }

            _kafkaConsumer.Unsubscribe();

            await base.StopAsync(cancellationToken);

            _logger.LogInformation("// {Class} // Unsubscribed from topic {Topic} because shutdown is initiated ", GetType().Name, _topicFingerprint);

            if (!IsConsumerClosed)
            {
                SignalConsumerClosure();

                _kafkaConsumer.Close();
            }
        }

        /// <inheritdoc/>
        protected override abstract Task ExecuteAsync(CancellationToken stoppingToken);

        /// <summary>
        /// Consumes messages from the configured Kafka topic using batch polling and bounded parallel processing.
        /// </summary>
        /// <param name="processMessageFunc">
        /// Delegate that processes a single message value. Exceptions trigger a retry via <paramref name="retryMessageFunc"/>.
        /// </param>
        /// <param name="retryMessageFunc">
        /// Delegate invoked when <paramref name="processMessageFunc"/> fails. If it also fails, the batch stops launching new processing tasks.
        /// </param>
        /// <param name="cancellationToken">
        /// Token observed for cooperative cancellation. When signaled, polling and new task launches stop and in-flight tasks are awaited.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous consume operation. The task completes when cancellation is requested or shutdown is initiated.
        /// </returns>
        protected async Task ConsumeMessageAsync(Func<string, Task> processMessageFunc, Func<string, Task> retryMessageFunc, CancellationToken cancellationToken)
        {
            using var linkedCancellationTokenSource = _internalCancellationSource is null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _internalCancellationSource.Token);

            var linkedCancellationToken = linkedCancellationTokenSource.Token;

            while (!linkedCancellationToken.IsCancellationRequested && !IsShutdownStarted && !IsConsumerClosed)
            {
                ResetBatchProcessingFailureSignal();

                var batchProcessingTimer = Stopwatch.StartNew();

                var polledConsumeResults = PollConsumeResults(linkedCancellationToken);
                if (polledConsumeResults.Count == 0)
                {
                    batchProcessingTimer.Stop();

                    try
                    {
                        await Task.Delay(50, linkedCancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                _messagesPolledCounter.Add(polledConsumeResults.Count, KeyValuePair.Create<string, object?>(_metricsTopicTag, _topicFingerprint));

                var processedOffsets = await ProcessConsumeResultsAsync(polledConsumeResults, processMessageFunc, retryMessageFunc, linkedCancellationToken);

                _lastProcessedBatch = new KafkaBatchState
                {
                    CommitReadyOffsets = [.. processedOffsets],
                    PolledConsumeResults = [.. polledConsumeResults]
                };

                var normalizedOffsets = CalculateContiguousCommitOffsets(processedOffsets, polledConsumeResults);
                if (normalizedOffsets.Count > 0)
                {
                    CommitNormalizedOffsets(normalizedOffsets);
                }

                batchProcessingTimer.Stop();

                _batchProcessingLatency.Record(batchProcessingTimer.Elapsed.TotalMilliseconds, KeyValuePair.Create<string, object?>(_metricsTopicTag, _topicFingerprint));
            }
        }

        /// <summary>
        /// Indicates whether the consumer has been closed.
        /// </summary>
        private bool IsConsumerClosed => Volatile.Read(ref _consumerClosedFlag) != 0;

        /// <summary>
        /// Indicates whether consumer shutdown has been started.
        /// </summary>
        private bool IsShutdownStarted => Volatile.Read(ref _shutdownStartedFlag) != 0;

        /// <summary>
        /// Indicates whether a message processing failure has occurred in the current batch.
        /// </summary>
        private bool IsMessageProcessingFailureSignaled => Volatile.Read(ref _batchFailureFlag) != 0;

        /// <summary>
        /// Computes a deterministic truncated SHA-256 hexadecimal fingerprint for a Kafka topic name.
        /// </summary>
        /// <param name="topicName">
        /// The original Kafka topic name to fingerprint. 
        /// If <c>null</c>, empty, or whitespace, the literal string <c>"EMPTY"</c> is returned.
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

            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(topicName));
            string hex = Convert.ToHexString(digest.AsSpan(0, 8));

            return hex.ToLowerInvariant();
        }

        /// <summary>
        /// Builds the Kafka <see cref="ConsumerConfig"/> using the shared client configuration.
        /// </summary>
        /// <param name="settings">The configuration object used to hold integration settings for Kafka.</param>
        /// <returns>A fully initialized <see cref="ConsumerConfig"/> ready to be used by a <see cref="ConsumerBuilder{TKey, TValue}"/>.</returns>
        private ConsumerConfig BuildConfiguration(IOptions<KafkaSettings> settings)
        {
            var configuration = new SharedClientConfig(settings.Value);

            var consumerConfig = new ConsumerConfig(configuration.ConsumerSettings)
            {
                FetchWaitMaxMs = 100,
                QueuedMinMessages = 100,
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
                GroupId = $"{settings.Value.Consumer.GroupId}-{GetType().Name.ToLower()}"
            };

            return consumerConfig;
        }

        /// <summary>
        /// Creates and configures a Kafka consumer instance.
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
                    if (IsShutdownStarted || IsConsumerClosed)
                    {
                        return;
                    }

                    var lastBatchSafeOffsets = _lastProcessedBatch != null
                        ? CalculateContiguousCommitOffsets(_lastProcessedBatch.CommitReadyOffsets, _lastProcessedBatch.PolledConsumeResults)
                        : [];

                    var revokedPartitionOffsets = lastBatchSafeOffsets
                        .Where(offsetEntry => partitions.Any(revokedPartition => revokedPartition.TopicPartition.Equals(offsetEntry.TopicPartition)))
                        .ToList();

                    if (revokedPartitionOffsets.Count == 0)
                    {
                        return;
                    }

                    try
                    {
                        _kafkaConsumer.Commit(revokedPartitionOffsets);
                    }
                    catch (KafkaException ex) when (ex.Error.Code is ErrorCode.RebalanceInProgress or ErrorCode.IllegalGeneration)
                    {
                        _logger.LogWarning(ex, "// {Class} // Commit on revocation skipped due to transient state: {Reason}", GetType().Name, ex.Error.Reason);
                    }
                    catch (KafkaException ex)
                    {
                        _logger.LogError(ex, "// {Class} // Commit on revocation failed unexpectedly", GetType().Name);
                    }

                    _logger.LogInformation("// {Class} // Partitions revoked: {Partitions}", GetType().Name, string.Join(',', partitions.Select(e => e.Partition.Value)));
                })
                .SetPartitionsAssignedHandler((_, partitions) =>
                {
                    _logger.LogInformation("// {Class} // Partitions assigned: {Partitions}", GetType().Name, string.Join(',', partitions.Select(e => e.Partition.Value)));
                })
                .Build();
        }

        /// <summary>
        /// Commits normalized offsets to Kafka.
        /// Automatically normalizes multiple offsets per partition to the highest value, ensuring safe offset advancement
        /// and gracefully handling transient Kafka consumer group states during rebalancing operations.
        /// </summary>
        /// <param name="offsetsToCommit">
        /// Collection of next-position offsets (original message offset + 1) ready for commit to Kafka.
        /// May contain multiple offset entries per partition from concurrent message processing, which are automatically
        /// normalized to ensure only the highest safe offset per partition is committed to Kafka.
        /// </param>
        private void CommitNormalizedOffsets(List<TopicPartitionOffset> offsetsToCommit)
        {
            if (offsetsToCommit is null || offsetsToCommit.Count == 0 || IsShutdownStarted || IsConsumerClosed)
            {
                return;
            }

            var normalizedOffsetsPerPartition = offsetsToCommit
                .GroupBy(e => e.TopicPartition)
                .Select(e =>
                {
                    var maxOffset = e.Select(x => x.Offset.Value).Max();
                    return new TopicPartitionOffset(e.Key, new Offset(maxOffset));
                })
                .ToList();

            try
            {
                _kafkaConsumer.Commit(normalizedOffsetsPerPartition);
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
        /// Atomically signals that the consumer closure.
        /// </summary>
        private void SignalConsumerClosure() => Interlocked.Exchange(ref _consumerClosedFlag, 1);

        /// <summary>
        /// Atomically signals that consumer shutdown has been initiated.
        /// </summary>
        private void SignalShutdownStarted() => Interlocked.Exchange(ref _shutdownStartedFlag, 1);

        /// <summary>
        /// Atomically signals that a batch processing failure has occurred.
        /// </summary>
        private void SignalBatchProcessingFailure() => Interlocked.Exchange(ref _batchFailureFlag, 1);

        /// <summary>
        /// Atomically clears the batch processing failure signal before handling a new batch.
        /// </summary>
        private void ResetBatchProcessingFailureSignal() => Interlocked.Exchange(ref _batchFailureFlag, 0);

        /// <summary>
        /// Polls the Kafka consumer for new messages until either the time budget or the per-batch item cap is reached, or shutdown/cancellation is observed.
        /// </summary>
        /// <param name="cancellationToken">Token observed for cooperative cancellation and shutdown.</param>
        /// <returns>
        /// A list of <see cref="ConsumeResult{TKey, TValue}"/> containing the polled consume results. The list may be empty.
        /// </returns>
        private List<ConsumeResult<string, string>> PollConsumeResults(CancellationToken cancellationToken)
        {
            var batchPollingDeadline = Environment.TickCount64 + _batchPollTimeoutMs;
            var batchMessageCollection = new List<ConsumeResult<string, string>>(_maxBatchSize);

            while (!cancellationToken.IsCancellationRequested && !IsShutdownStarted && !IsConsumerClosed)
            {
                var remainingTimeoutMs = (int)Math.Max(0, batchPollingDeadline - Environment.TickCount64);
                if (remainingTimeoutMs <= 0)
                {
                    break;
                }

                if (batchMessageCollection.Count >= _maxBatchSize)
                {
                    break;
                }

                try
                {
                    var polledMessage = _kafkaConsumer.Consume(TimeSpan.FromMilliseconds(remainingTimeoutMs));
                    if (polledMessage is null)
                    {
                        break;
                    }

                    batchMessageCollection.Add(polledMessage);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "// {Class} // Exception during polling", GetType().Name);
                    break;
                }
            }

            return [.. batchMessageCollection];
        }

        /// <summary>
        /// Calculates the highest contiguous commit offsets for each partition by identifying the longest sequence 
        /// of successfully processed messages starting from the earliest polled offset in the batch.
        /// </summary>
        /// <param name="commitReadyOffsets">
        /// The immutable collection of successfully processed message offsets ready for commit.
        /// Contains the next-position offsets (original offset + 1) from all messages that completed processing successfully.
        /// </param>
        /// <param name="polledConsumeResults">
        /// The immutable collection of original Kafka consume results that were polled from the topic.
        /// Used to determine the polling order and establish contiguous commit boundaries by partition.
        /// </param>
        /// <returns>
        /// A list of <see cref="TopicPartitionOffset"/> representing the highest safe commit position for each partition.
        /// Only includes partitions where at least one message from the beginning of the batch was successfully processed.
        /// Returns an empty list when no contiguous processing success can be established from batch start for any partition.
        /// Each offset represents the next position to read from (original message offset + 1) following Kafka commit semantics.
        /// </returns>
        private static List<TopicPartitionOffset> CalculateContiguousCommitOffsets(List<TopicPartitionOffset> commitReadyOffsets, List<ConsumeResult<string, string>> polledConsumeResults)
        {
            var safeOffsetsToCommit = new List<TopicPartitionOffset>();

            var polledOffsetsByPartition = polledConsumeResults
                .GroupBy(cr => cr.TopicPartition)
                .ToDictionary(grp => grp.Key, grp => grp.Select(cr => cr.Offset.Value).OrderBy(offset => offset).ToList());

            var processedOffsetsByPartition = commitReadyOffsets
                .GroupBy(tpo => tpo.TopicPartition)
                .ToDictionary(grp => grp.Key, grp => new HashSet<long>(grp.Select(tpo => tpo.Offset.Value)));

            foreach (var partition in polledOffsetsByPartition)
            {
                var topicPartition = partition.Key;
                var orderedOffsets = partition.Value;

                if (!processedOffsetsByPartition.TryGetValue(topicPartition, out var successSet) || successSet.Count == 0)
                {
                    continue;
                }

                long? safeCommitOffset = null;

                foreach (var offset in orderedOffsets)
                {
                    var nextPosition = offset + 1;

                    if (successSet.Contains(nextPosition))
                    {
                        safeCommitOffset = nextPosition;
                    }
                    else
                    {
                        break;
                    }
                }

                if (safeCommitOffset.HasValue)
                {
                    safeOffsetsToCommit.Add(new TopicPartitionOffset(topicPartition, new Offset(safeCommitOffset.Value)));
                }
            }

            return safeOffsetsToCommit;
        }

        /// <summary>
        /// Processes an individual Kafka message with automatic retry logic, comprehensive error handling, and batch processing coordination.
        /// </summary>
        /// <param name="kafkaMessage">
        /// The Kafka consume result containing the message data, offset, partition, and metadata for processing.
        /// </param>
        /// <param name="primaryProcessorFunc">
        /// Primary message processing delegate that handles the core business logic for the message value. 
        /// When this delegate throws an exception, the message is automatically retried using <paramref name="retryProcessorFunc"/>.
        /// </param>
        /// <param name="retryProcessorFunc">
        /// Fallback processing delegate invoked when <paramref name="primaryProcessorFunc"/> fails. 
        /// If this delegate also fails, a batch processing failure signal is set to halt further processing in the current iteration.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token for cooperative shutdown. Processing is short-circuited immediately when cancellation is requested,
        /// consumer shutdown is initiated, consumer is closed, or a batch processing failure has been signaled.
        /// </param>
        /// <returns>
        /// A <see cref="TopicPartitionOffset"/> representing the next offset to commit (original offset + 1) 
        /// if message processing succeeds through either primary or retry processing pathways;
        /// otherwise <c>null</c> if processing fails completely, is short-circuited due to shutdown conditions, 
        /// or is skipped due to batch processing failure signals.
        /// </returns>
        private async Task<TopicPartitionOffset?> ProcessMessageAsync(ConsumeResult<string, string> kafkaMessage, Func<string, Task> primaryProcessorFunc, Func<string, Task> retryProcessorFunc, CancellationToken cancellationToken)
        {
            try
            {
                if (kafkaMessage.Message.Value is null)
                {
                    _logger.LogWarning("// {Class} // Skipping message with null value at offset {Offset}", GetType().Name, kafkaMessage.Offset);
                    return new TopicPartitionOffset(kafkaMessage.TopicPartition, kafkaMessage.Offset + 1);
                }

                if (cancellationToken.IsCancellationRequested || IsMessageProcessingFailureSignaled)
                {
                    return null;
                }

                await primaryProcessorFunc(kafkaMessage.Message.Value);

                _messagesProcessedCounter.Add(1, KeyValuePair.Create<string, object?>(_metricsTopicTag, _topicFingerprint));

                return new TopicPartitionOffset(kafkaMessage.TopicPartition, kafkaMessage.Offset + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// {Class} // Error processing message at offset {Offset}, attempting retry", GetType().Name, kafkaMessage.Offset);

                try
                {
                    if (cancellationToken.IsCancellationRequested || IsMessageProcessingFailureSignaled)
                    {
                        return null;
                    }

                    await retryProcessorFunc(kafkaMessage.Message.Value);

                    _retrySuccessCounter.Add(1, KeyValuePair.Create<string, object?>(_metricsTopicTag, _topicFingerprint));

                    return new TopicPartitionOffset(kafkaMessage.TopicPartition, kafkaMessage.Offset + 1);
                }
                catch (Exception retryEx)
                {
                    SignalBatchProcessingFailure();

                    _logger.LogError(retryEx, "// {Class} // Retry failed for message at offset {Offset}. Halting further launches.", GetType().Name, kafkaMessage.Offset);

                    _retryFailureCounter.Add(1, KeyValuePair.Create<string, object?>(_metricsTopicTag, _topicFingerprint));

                    return null;
                }
            }
        }

        /// <summary>
        /// Processes a batch of Kafka messages concurrently using parallel execution with built-in retry logic and error handling.
        /// Each message is processed independently, and successfully processed messages contribute their offsets to the commit-ready collection.
        /// Processing stops gracefully when cancellation is requested or the consumer is shutting down.
        /// </summary>
        /// <param name="consumeResults">
        /// The immutable collection of Kafka consume results to be processed concurrently.
        /// </param>
        /// <param name="processFunc">
        /// Primary message processing delegate. When this delegate throws an exception, the message is automatically retried using <paramref name="retryFunc"/>.
        /// </param>
        /// <param name="retryFunc">
        /// Retry processing delegate invoked when <paramref name="processFunc"/> fails. If this delegate also fails, 
        /// a batch processing failure is signaled to prevent further message processing in subsequent iterations.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token for cooperative shutdown. When signaled, no new message processing tasks are started, 
        /// but in-flight tasks are allowed to complete naturally.
        /// </param>
        /// <returns>
        /// An immutable collection of <see cref="TopicPartitionOffset"/> representing the next-offsets (original offset + 1) 
        /// from all successfully processed messages. Messages that failed both primary and retry processing are not included.
        /// </returns>
        private async Task<List<TopicPartitionOffset>> ProcessConsumeResultsAsync(List<ConsumeResult<string, string>> consumeResults, Func<string, Task> processFunc, Func<string, Task> retryFunc, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || IsShutdownStarted || IsConsumerClosed)
            {
                return [];
            }

            var concurrencyConfiguration = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                TaskScheduler = TaskScheduler.Default,
                MaxDegreeOfParallelism = Math.Min(_maxBatchSize, Environment.ProcessorCount * 2)
            };

            var processedMessageOffsets = new ConcurrentBag<TopicPartitionOffset>();

            try
            {
                await Parallel.ForEachAsync(
                    consumeResults,
                    concurrencyConfiguration,
                    async (consumeResult, stoppingToken) =>
                    {
                        if (cancellationToken.IsCancellationRequested || IsShutdownStarted || IsConsumerClosed || IsMessageProcessingFailureSignaled)
                        {
                            return;
                        }

                        var processedOffset = await ProcessMessageAsync(consumeResult, processFunc, retryFunc, stoppingToken);
                        if (processedOffset != null)
                        {
                            processedMessageOffsets.Add(processedOffset);
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }

            return [.. processedMessageOffsets];
        }
    }
}
