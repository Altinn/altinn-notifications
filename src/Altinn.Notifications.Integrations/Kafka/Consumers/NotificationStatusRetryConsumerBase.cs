using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Serves as a base class for implementing consumers that handle status updates with retry logic.
/// </summary>
/// <remarks>This abstract class provides the core retry mechanism for processing status updates.
/// It manages retry timing, dead delivery report persistence, and message requeuing. Derived classes
/// must implement the <see cref="Channel"/> property to specify the delivery channel and override
/// the ExecuteAsync method to consume messages from their respective Kafka topics.</remarks>
public abstract class NotificationStatusRetryConsumerBase(
        IKafkaProducer producer,
        IDeadDeliveryReportService deadDeliveryReportService,
        IOptions<KafkaSettings> settings,
        string topicName,
        ILogger<NotificationStatusRetryConsumerBase> logger) : KafkaConsumerBase<NotificationStatusRetryConsumerBase>(settings, logger, topicName)
{
    private readonly IKafkaProducer _producer = producer;
    private readonly IDeadDeliveryReportService _deadDeliveryReportService = deadDeliveryReportService;
    private readonly string _topicName = topicName;
    private readonly int _statusUpdateRetrySeconds = settings.Value.StatusUpdatedRetryThresholdSeconds;
    private readonly ILogger _logger = logger;

    /// <summary>
    /// Gets the delivery report channel for this consumer.
    /// </summary>
    protected abstract DeliveryReportChannel Channel { get; }

    /// <summary>
    /// Processes a status update message, determining whether to retry a dead delivery report based on elapsed time.
    /// </summary>
    /// <param name="message">The message containing the retry message data, including the delivery report from the provider</param>
    /// <returns></returns>
    protected async Task ProcessStatus(string message)
    {
        var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options);

        if (retryMessage == null)
        {
            _logger.LogError("Deserialization of message failed. {Message}", message);

            // putting this message back on the topic would cause an infinite loop since it will fail deserialization every time
            // we log the error abd return
            return;
        }
        
        var elapsedSeconds = (DateTime.UtcNow - retryMessage.FirstSeen).TotalSeconds;

        if (elapsedSeconds >= _statusUpdateRetrySeconds)
        {
            _logger.LogInformation("Processing retry message after {ElapsedSeconds} seconds", elapsedSeconds);

            // Persist the dead delivery report after hitting the retry threshold
            var deadDeliveryReport = new DeadDeliveryReport
            {
                AttemptCount = retryMessage.Attempts,
                Channel = Channel,
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

            _logger.LogInformation(
                "Message not ready for retry. Elapsed: {ElapsedSeconds}s, Threshold: {ThresholdSeconds}s",
                elapsedSeconds,
                _statusUpdateRetrySeconds);

            await _producer.ProduceAsync(_topicName, incrementedRetryMessage.Serialize());
        }
    }

    /// <summary>
    /// Sends a message to the retry topic.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_topicName, message);
    }
}
