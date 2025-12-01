using Altinn.Notifications.Integrations.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Base class for Kafka Consumer messages
/// </summary>
public abstract class KafkaConsumerBase : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topicName;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerBase"/> class.
    /// </summary>
    protected KafkaConsumerBase(
           IOptions<KafkaSettings> settings,
           ILogger logger,
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
        ConsumeResult<string, string>? consumeResult;
        string message;

        while (!stoppingToken.IsCancellationRequested)
        {
            message = string.Empty;
            consumeResult = null;

            try
            {
                consumeResult = _consumer.Consume(stoppingToken);
                if (consumeResult != null)
                {
                    message = consumeResult.Message.Value;
                    await processMessageFunc(message);
                    CommitOffset(consumeResult);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellationToken is canceled
            }
            catch (Exception ex)
            {
                await HandleProcessingFailure(consumeResult, message, retryMessageFunc, ex);
            }
        }
    }

    /// <summary>
    /// Handles message processing failures by attempting retry and committing offset on success.
    /// </summary>
    /// <param name="consumeResult">The Kafka consume result, or null if message consumption failed.</param>
    /// <param name="message">The message that failed to process.</param>
    /// <param name="retryMessageFunc">Function to retry the message (e.g., republish to retry topic).</param>
    /// <param name="ex">The exception that occurred during processing.</param>
    /// <remarks>
    /// Only commits the offset if retry succeeds. If retry fails, the message will be reprocessed
    /// on the next consumer restart, ensuring at-least-once delivery.
    /// </remarks>
    private async Task HandleProcessingFailure(
        ConsumeResult<string, string>? consumeResult,
        string message,
        Func<string, Task> retryMessageFunc,
        Exception ex)
    {
        if (consumeResult == null)
        {
            _logger.LogError(ex, "// {Class} // ConsumeMessage // An error occurred while consuming messages", GetType().Name);
            return;
        }

        bool retrySucceeded = await TryRetryMessage(message, retryMessageFunc);

        if (retrySucceeded)
        {
            CommitOffset(consumeResult);
        }
        else
        {
            _logger.LogError(ex, "// {Class} // ConsumeMessage // An error occurred while consuming messages", GetType().Name);
        }
    }

    /// <summary>
    /// Attempts to retry a failed message and returns whether the retry succeeded.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <param name="retryMessageFunc">Function to execute the retry logic.</param>
    /// <returns>True if retry succeeded; false if it failed.</returns>
    private async Task<bool> TryRetryMessage(string message, Func<string, Task> retryMessageFunc)
    {
        try
        {
            await retryMessageFunc(message);
            return true;
        }
        catch (Exception retryEx)
        {
            _logger.LogError(retryEx, "// {Class} // ConsumeMessage // An error occurred while retrying message processing", GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Commits the Kafka offset for a successfully processed message.
    /// This tells Kafka the message has been handled and won't be reprocessed.
    /// </summary>
    /// <param name="consumeResult">The consume result containing the offset to commit.</param>
    private void CommitOffset(ConsumeResult<string, string> consumeResult)
    {
        _consumer.Commit(consumeResult);
        _consumer.StoreOffset(consumeResult);
    }
}
