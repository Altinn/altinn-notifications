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
    private readonly ILogger<KafkaConsumerBase<T>> _logger;
    private readonly IConsumer<string, string> _consumer;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerBase{T}"/> class.
    /// </summary>
    protected KafkaConsumerBase(
           IOptions<KafkaSettings> settings,
           ILogger<KafkaConsumerBase<T>> logger,
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
            .SetErrorHandler((_, e) => _logger.LogError($"// {GetType().Name} // Error: {e.Reason}"))
            .Build();

        _consumer.Subscribe(topicName);
    }

    /// <inheritdoc/>
    protected abstract override Task ExecuteAsync(CancellationToken stoppingToken);

    /// <inheritdoc/>
    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();

        base.Dispose();
    }

    /// <summary>
    /// Consuming a notification order from kafka topic and calling processing or retry function
    /// </summary>
    protected async Task ConsumeOrder(
        Func<NotificationOrder, Task> processOrder,
        Func<string, Task> retryOrder,
        CancellationToken stoppingToken)
    {
        string message = string.Empty;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = _consumer.Consume(stoppingToken);
                if (consumeResult != null)
                {
                    message = consumeResult.Message.Value;
                    bool succeeded = NotificationOrder.TryParse(message, out NotificationOrder? order);

                    if (!succeeded)
                    {
                        continue;
                    }

                    await processOrder(order!);
                    _consumer.Commit(consumeResult);
                    _consumer.StoreOffset(consumeResult);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellationToken is canceled
        }
        catch (Exception ex)
        {
            await retryOrder(message!);
            _logger.LogError(ex, $"// {GetType().Name} // ConsumeOrder // An error occurred while consuming messages");
        }
    }
}