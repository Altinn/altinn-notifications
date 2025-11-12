using System.Collections.Concurrent;

using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Base class for Kafka Consumer messages
/// </summary>
public abstract class KafkaConsumerBase<T> : BackgroundService
    where T : class
{
    private readonly string _topicName;
    private readonly ILogger<T> _logger;
    private readonly IConsumer<string, string> _consumer;
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

        var config = new SharedClientConfig(settings.Value);

        var consumerConfig = new ConsumerConfig(config.ConsumerSettings)
        {
            GroupId = $"{settings.Value.Consumer.GroupId}-{GetType().Name.ToLower()}",
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogError("// {Class} // Error: {Reason}", GetType().Name, e.Reason))
            .Build();
        _topicName = topicName;
    }

    /// <inheritdoc/>
    protected override abstract Task ExecuteAsync(CancellationToken stoppingToken);

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_topicName);
        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Close and dispose the consumer
    /// </summary>
    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Consuming a message from kafka topic and calling processing and potentially retry function
    /// </summary>
    protected async Task ConsumeMessage(
        Func<string, Task> processMessageFunc,
        Func<string, Task> retryMessageFunc,
        CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(stoppingToken);
                if (consumeResult == null)
                {
                    continue;
                }

                var topicPartition = consumeResult.TopicPartition;
                var semaphore = _partitionLocks.GetOrAdd(topicPartition, _ => new SemaphoreSlim(1, 1));
                _ = Task.Run(
                        async () =>
                        {
                            await semaphore.WaitAsync(stoppingToken).ConfigureAwait(false);
                            try
                            {
                                string message = consumeResult.Message.Value;
                                await processMessageFunc(message).ConfigureAwait(false);

                                _consumer.Commit(consumeResult);
                                _consumer.StoreOffset(consumeResult);
                            }
                            catch (OperationCanceledException)
                            {
                                // Expected when cancellationToken is canceled
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    await retryMessageFunc(consumeResult.Message.Value).ConfigureAwait(false);

                                    if (consumeResult != null)
                                    {
                                        _consumer.Commit(consumeResult);
                                        _consumer.StoreOffset(consumeResult);
                                    }
                                }
                                catch (Exception retryEx)
                                {
                                    _logger.LogError(retryEx, "// {Class} // Retry failed for message", GetType().Name);
                                }

                                _logger.LogError(ex, "// {Class} // ConsumeMessage // Error while processing message", GetType().Name);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        },
                        stoppingToken);
            }
            catch (ConsumeException ce) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ce, "// {Class} // Kafka consume exception", GetType().Name);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "// {Class} // Unexpected loop error", GetType().Name);
            }
        }
    }
}
