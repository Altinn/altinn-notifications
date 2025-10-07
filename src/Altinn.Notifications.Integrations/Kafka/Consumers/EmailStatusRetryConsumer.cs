using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing email status retry messages
/// </summary>
public sealed class EmailStatusRetryConsumer(IKafkaProducer producer, IDeadDeliveryReportService deadDeliveryReportService, IOptions<Configuration.KafkaSettings> settings, ILogger<EmailStatusRetryConsumer> logger)
    : KafkaConsumerBase<EmailStatusRetryConsumer>(settings, logger, settings.Value.EmailStatusUpdatedRetryTopicName)
{
    private readonly IKafkaProducer _producer = producer;
    private readonly IDeadDeliveryReportService _deadDeliveryReportService = deadDeliveryReportService;
    private readonly ILogger<EmailStatusRetryConsumer> _logger = logger;
    private readonly string _retryTopicName = settings.Value.EmailStatusUpdatedRetryTopicName;
    private readonly int _statusUpdateRetrySeconds = settings.Value.StatusUpdatedRetryThresholdSeconds;
    private readonly DeliveryReportChannel _channel = DeliveryReportChannel.AzureCommunicationServices;

    /// <summary>
    /// Executes the email status retry consumer to process messages from the Kafka topic
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the consumer</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    private async Task ProcessStatus(string message)
    {
        var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message) ?? throw new InvalidOperationException("Could not deserialize message");
        
        var elapsedSeconds = (DateTime.UtcNow - retryMessage.FirstSeen).TotalSeconds;
    
        if (elapsedSeconds >= _statusUpdateRetrySeconds)
        {
            _logger.LogInformation("Processing retry message after {ElapsedSeconds} seconds", elapsedSeconds);

            // Persist the dead delivery report after hitting the retry threshold
            var deadDeliveryReport = new DeadDeliveryReport
            {
                AttemptCount = retryMessage.Attempts,
                Channel = _channel,
                FirstSeen = retryMessage.FirstSeen,
                LastAttempt = DateTime.UtcNow,
                Resolved = false,
                DeliveryReport = retryMessage.SendResult ?? string.Empty
            };

            await _deadDeliveryReportService.InsertAsync(deadDeliveryReport);
        }
        else
        {
            // increment retries before putting it back on the retry topic
            var incrementedRetryMessage = retryMessage with { Attempts = retryMessage.Attempts + 1 };

            _logger.LogDebug(
                "Message not ready for retry. Elapsed: {ElapsedSeconds}s, Threshold: {ThresholdSeconds}s",
                elapsedSeconds,
                _statusUpdateRetrySeconds);

            await _producer.ProduceAsync(_retryTopicName, incrementedRetryMessage.Serialize());
        }
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
