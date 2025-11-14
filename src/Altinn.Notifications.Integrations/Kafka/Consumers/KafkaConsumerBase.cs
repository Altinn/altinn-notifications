using System.Collections.Concurrent;

using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Abstract base class for Kafka consumers, providing a framework for consuming messages from a specified topic.
/// Supports per-partition serialized processing with cross-partition concurrency, graceful shutdown, and error handling.
/// </summary>
public abstract class KafkaConsumerBase<T> : BackgroundService
    where T : class
{
    private volatile bool _stopping;
    private readonly string _topicName;
    private readonly ILogger<T> _logger;
    private const int _defaultMaxParallelism = 6;
    private readonly SemaphoreSlim _globalConcurrency;
    private readonly IConsumer<string, string> _consumer;
    private readonly ConcurrentDictionary<Guid, Task> _inFlightTasks = new();
    private readonly ConcurrentDictionary<TopicPartition, SemaphoreSlim> _partitionLocks = new();

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
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
            GroupId = $"{settings.Value.Consumer.GroupId}-{topicName.Replace('.', '-')}"
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) =>
            {
                _logger.LogError("// {Class} // Error: {Reason}", GetType().Name, e.Reason);
            })
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation("// {Class} // Partitions assigned: {Partitions}", GetType().Name, string.Join(',', partitions.Select(e => e.Partition.Value)));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogInformation("// {Class} // Partitions revoked: {Partitions}", GetType().Name, string.Join(',', partitions.Select(e => e.Partition.Value)));
            })
            .Build();

        _globalConcurrency = new SemaphoreSlim(_defaultMaxParallelism, _defaultMaxParallelism);
    }

    /// <inheritdoc/>
    protected override abstract Task ExecuteAsync(CancellationToken stoppingToken);

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_topicName);
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;

        _consumer.Unsubscribe();

        Task[] tasks = [.. _inFlightTasks.Values];
        if (tasks.Length > 0)
        {
            _logger.LogInformation("// {Class} // Waiting for {Count} in-flight tasks to complete", GetType().Name, tasks.Length);

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// {Class} // Error while awaiting in-flight tasks during shutdown", GetType().Name);
            }
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Safely commits the offset for a processed message.
    /// </summary>
    private void SafeCommit(ConsumeResult<string, string> result)
    {
        if (_stopping)
        {
            return;
        }

        try
        {
            _consumer.Commit(result);
        }
        catch (KafkaException ex)
        {
            if (ex.Error.Code is ErrorCode.RebalanceInProgress or ErrorCode.IllegalGeneration)
            {
                _logger.LogWarning("// {Class} // Commit skipped due to transient state: {Reason}", GetType().Name, ex.Error.Reason);
            }
            else
            {
                _logger.LogError(ex, "// {Class} // Commit failed unexpectedly", GetType().Name);
            }
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Consumes messages from the Kafka topic in a continuous loop, ensuring per-partition serialized processing
    /// while allowing cross-partition concurrency. Messages are polled, processed via the provided delegate,
    /// and committed on success. On processing failure, the retry delegate is invoked. The loop respects
    /// global concurrency limits and handles graceful shutdown via the cancellation token.
    /// </summary>
    /// <param name="processMessageFunc">A function that takes the message string and processes it asynchronously.</param>
    /// <param name="retryMessageFunc">A function that takes the message string and handles retry logic asynchronously on processing failure.</param>
    /// <param name="stoppingToken">A cancellation token to signal when to stop consuming messages.</param>
    protected async Task ConsumeMessage(
        Func<string, Task> processMessageFunc,
        Func<string, Task> retryMessageFunc,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !_stopping)
        {
            ConsumeResult<string, string>? consumeResult = null;

            try
            {
                consumeResult = _consumer.Consume(stoppingToken);
                if (consumeResult == null)
                {
                    continue;
                }

                var topicPartition = consumeResult.TopicPartition;
                var partitionSemaphore = _partitionLocks.GetOrAdd(topicPartition, _ => new SemaphoreSlim(1, 1));

                await _globalConcurrency.WaitAsync(stoppingToken).ConfigureAwait(false);

                var processingTaskIdentifier = Guid.NewGuid();
                var processingTask = Task.Run(
                    async () =>
                    {
                        await partitionSemaphore.WaitAsync(stoppingToken).ConfigureAwait(false);
                        try
                        {
                            string message = consumeResult.Message.Value;

                            await processMessageFunc(message).ConfigureAwait(false);

                            SafeCommit(consumeResult);
                        }
                        catch (OperationCanceledException)
                        {
                            // shutdown scenario
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                await retryMessageFunc(consumeResult.Message.Value).ConfigureAwait(false);

                                SafeCommit(consumeResult);
                            }
                            catch (Exception retryEx)
                            {
                                _logger.LogError(retryEx, "// {Class} // Retry failed for message", GetType().Name);
                            }

                            _logger.LogError(ex, "// {Class} // ConsumeMessage // Error while processing message", GetType().Name);
                        }
                        finally
                        {
                            partitionSemaphore.Release();
                            _globalConcurrency.Release();
                            _inFlightTasks.TryRemove(processingTaskIdentifier, out _);
                        }
                    },
                    stoppingToken);

                _inFlightTasks[processingTaskIdentifier] = processingTask;
            }
            catch (ConsumeException ce) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ce, "// {Class} // Kafka consume exception", GetType().Name);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// {Class} // Unexpected loop error", GetType().Name);
            }
        }
    }
}
