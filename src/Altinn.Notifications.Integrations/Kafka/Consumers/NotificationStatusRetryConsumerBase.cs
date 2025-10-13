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
        UpdateStatusRetryMessage? retryMessage;
        try
        {
            retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Deserialization of message failed due to malformed JSON. {Message}", message);
            return;
        }

        if (retryMessage == null)
        {
            _logger.LogError("Deserialization of message returned null. {Message}", message);

            // putting this message back on the topic would cause an infinite loop since it will fail deserialization every time
            // we log the error and return
            return;
        }
        
        var elapsedSeconds = (DateTime.UtcNow - retryMessage.FirstSeen).TotalSeconds;

        if (elapsedSeconds >= _statusUpdateRetrySeconds)
        {
            await PersistDeadDeliveryReport(retryMessage, elapsedSeconds);
        }
        else
        {
            await AttemptToUpdateSendStatus(retryMessage);
        }
    }

    /// <summary>
    /// Updates the notification based on the retryMessage
    /// </summary>
    /// <remarks>This method is abstract and must be implemented by a derived class. The implementation should
    /// handle the logic for updating the status based on the provided <paramref name="retryMessage"/>.</remarks>
    /// <param name="retryMessage">The message containing retry information and the context required to update the status.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task UpdateStatusAsync(UpdateStatusRetryMessage retryMessage);

    /// <summary>
    /// Sends a message to the retry topic.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task RetryStatus(string message)
    {
        await _producer.ProduceAsync(_topicName, message);
    }

    private async Task PersistDeadDeliveryReport(UpdateStatusRetryMessage retryMessage, double elapsedSeconds)
    {
        _logger.LogInformation("Processing retry message after {ElapsedSeconds} seconds", elapsedSeconds);

        // Persist the dead delivery report after hitting the retry threshold
        var deadDeliveryReport = new DeadDeliveryReport
        {
            AttemptCount = retryMessage.Attempts,
            Channel = Channel,
            FirstSeen = retryMessage.FirstSeen,
            LastAttempt = retryMessage.LastAttempt,
            Resolved = false,
            DeliveryReport = retryMessage.SendResult ?? string.Empty
        };

        await _deadDeliveryReportService.InsertAsync(deadDeliveryReport);
    }

    private async Task AttemptToUpdateSendStatus(UpdateStatusRetryMessage retryMessage)
    {
        try
        {
            // Attempt to update the status
            await UpdateStatusAsync(retryMessage);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            _logger.LogWarning(e, "Deserialization of SendResult failed due to malformed JSON. Not retrying. {SendResult}", retryMessage.SendResult);
        }
        catch (Exception)
        {
            // increment retries before putting it back on the retry topic
            var incrementedRetryMessage = retryMessage with { Attempts = retryMessage.Attempts + 1, LastAttempt = DateTime.UtcNow };

            await RetryStatus(incrementedRetryMessage.Serialize());
        }
    }
}
