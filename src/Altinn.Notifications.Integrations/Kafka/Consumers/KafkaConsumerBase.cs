using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;

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
        string message = string.Empty;
        ConsumeResult<string, string>? consumeResult = null;

        while (!stoppingToken.IsCancellationRequested)
        {
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
                bool retrySucceeded;
                try
                {
                    await retryMessageFunc(message!);
                    retrySucceeded = true;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "// {Class} // ConsumeMessage // An error occurred while retrying message processing", GetType().Name);
                    retrySucceeded = false; // prevent offset commit
                }

                if (retrySucceeded && consumeResult != null)
                {
                    _consumer.Commit(consumeResult);
                    _consumer.StoreOffset(consumeResult);
                }

                _logger.LogError(ex, "// {Class} // ConsumeMessage // An error occurred while consuming messages", GetType().Name);
            }
        }
    }
}
