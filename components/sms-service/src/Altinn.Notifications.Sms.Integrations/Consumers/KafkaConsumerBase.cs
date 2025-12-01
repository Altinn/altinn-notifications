using Altinn.Notifications.Sms.Integrations.Configuration;
using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.Consumers;

/// <summary>
/// Base class for Kafka consumers
/// </summary>
public abstract class KafkaConsumerBase : BackgroundService
{
    private readonly ILogger<KafkaConsumerBase> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topicName;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerBase"/> class.
    /// </summary>
    protected KafkaConsumerBase(
           KafkaSettings settings,
           ILogger<KafkaConsumerBase> logger,
           string topicName)
    {
        _logger = logger;

        var config = new SharedClientConfig(settings);

        var consumerConfig = new ConsumerConfig(config.ConsumerConfig)
        {
            GroupId = $"{settings.Consumer.GroupId}-{GetType().Name.ToLower()}",
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
            string message = string.Empty;
            ConsumeResult<string, string>? consumeResult = null;

            try
            {
                consumeResult = _consumer.Consume(stoppingToken);
                if (consumeResult != null)
                {
                    message = consumeResult.Message.Value;
                    await processMessageFunc(message);
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

                _logger.LogError(ex, "// {Class} // ConsumeMessage // An error occurred while consuming messages", GetType().Name);
                if (consumeResult != null)
                {
                    _consumer.Commit(consumeResult);
                    _consumer.StoreOffset(consumeResult);
                }
            }
        }
    }
}
