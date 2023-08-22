using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Base class for Kafka Consumer of Notification Orders
/// </summary>
public abstract class KafkaConsumerBase<T> : BackgroundService
    where T : class
{
    private readonly ILogger<T> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topicName;

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

        var consumerConfig = new ConsumerConfig(config.ClientConfig)
        {
            GroupId = settings.Value.ConsumerGroupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogError("// { Class } // Error: { e.Reason }", GetType().Name, e.Reason))
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
        string message = string.Empty;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(stoppingToken);
                if (consumeResult != null)
                {
                    await processMessageFunc(consumeResult.Message.Value);
                    _consumer.Commit(consumeResult);
                    _consumer.StoreOffset(consumeResult);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellationToken is canceled
            }
            catch (Exception ex)
            {
                await retryMessageFunc(message!);
                _logger.LogError(ex, "// {Class} // ConsumeOrder // An error occurred while consuming messages", GetType().Name);
            }
        }
    }
}