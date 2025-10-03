using Altinn.Notifications.Core.Integrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing email status retry messages
/// </summary>
public sealed class EmailStatusRetryConsumer(IKafkaProducer producer, IOptions<Configuration.KafkaSettings> settings, ILogger<EmailStatusRetryConsumer> logger) 
    : KafkaConsumerBase<EmailStatusRetryConsumer>(settings, logger, settings.Value.EmailStatusUpdatedRetryTopicName)
{
    private readonly IKafkaProducer _producer = producer;
    private readonly ILogger<EmailStatusRetryConsumer> _logger = logger;
    private readonly string _retryTopicName = settings.Value.EmailStatusUpdatedRetryTopicName;

    /// <summary>
    /// Executes the email status retry consumer to process messages from the Kafka topic
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the consumer</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    private Task ProcessStatus(string message)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message to the retry topic.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_retryTopicName, message);
    }
}
