using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
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
public abstract class NotificationStatusRetryConsumerBase : KafkaConsumerBase<NotificationStatusRetryConsumerBase>
{
    private readonly ILogger _logger;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly int _statusRetryTimeoutInSeconds;
    private readonly string _statusUpdatedRetryTopicName;
    private readonly IDeadDeliveryReportService _deadDeliveryReportService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationStatusRetryConsumerBase"/> class.
    /// </summary>
    protected NotificationStatusRetryConsumerBase(
        string topicName,
        IKafkaProducer kafkaProducer,
        IOptions<KafkaSettings> kafkaSettings,
        IDeadDeliveryReportService deadDeliveryReportService,
        ILogger<NotificationStatusRetryConsumerBase> logger)
        : base(kafkaSettings, logger, topicName)
    {
        _logger = logger;
        _kafkaProducer = kafkaProducer;
        _statusUpdatedRetryTopicName = topicName;
        _deadDeliveryReportService = deadDeliveryReportService;
        _statusRetryTimeoutInSeconds = kafkaSettings.Value.StatusUpdatedRetryThresholdSeconds;
    }

    /// <summary>
    /// The communication channel (SMS or Email) this consumer is processing.
    /// </summary>
    protected abstract DeliveryReportChannel Channel { get; }

    /// <inheritdoc/>
    protected override sealed Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    /// <summary>
    /// Processes a status update message from Kafka, handling retry logic for delivery reports.
    /// </summary>
    /// <param name="message">The raw JSON string containing serialized retry message data from Kafka</param>
    /// <returns>A task representing the asynchronous processing operation</returns>
    protected async Task ProcessStatus(string message)
    {
        UpdateStatusRetryMessage? updateStatusRetryMessage = DeserializeMessage(message);
        if (updateStatusRetryMessage == null)
        {
            return;
        }

        var elapsedSeconds = (DateTime.UtcNow - updateStatusRetryMessage.FirstSeen).TotalSeconds;

        if (elapsedSeconds >= _statusRetryTimeoutInSeconds)
        {
            await PersistDeadDeliveryReport(updateStatusRetryMessage);
        }
        else
        {
            await ProcessStatusUpdateWithRetry(updateStatusRetryMessage);
        }
    }

    /// <summary>
    /// Updates the notification based on the retryMessage
    /// </summary>
    /// <param name="retryMessage">The message containing retry information and the context required to update the status.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task UpdateStatusAsync(UpdateStatusRetryMessage retryMessage);

    /// <summary>
    /// Sends a message to the same retry topic.
    /// </summary>
    /// <param name="message">The message to retry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task RetryStatus(string message)
    {
        await _kafkaProducer.ProduceAsync(_statusUpdatedRetryTopicName, message);
    }

    /// <summary>
    /// Deserializes a JSON message string into an <see cref="UpdateStatusRetryMessage"/> object.
    /// </summary>
    /// <param name="message">The JSON message string to deserialize.</param>
    /// <returns>
    /// The deserialized <see cref="UpdateStatusRetryMessage"/> object, or <c>null</c> if deserialization failed.
    /// </returns>
    private UpdateStatusRetryMessage? DeserializeMessage(string message)
    {
        try
        {
            var retryMessage = JsonSerializer.Deserialize<UpdateStatusRetryMessage>(message, JsonSerializerOptionsProvider.Options);

            if (retryMessage == null)
            {
                _logger.LogError("Message deserialization failed. {Message}", message);
                return null;
            }

            return retryMessage;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Message deserialization failed. {Message}", message);
            return null;
        }
    }

    /// <summary>
    /// Persists a failed delivery report as dead-delivery-report when the retry timeout has been exceeded.
    /// </summary>
    /// <param name="updateStatusRetryMessage">The retry message containing information about the failed delivery attempts.</param>
    /// <returns>A task representing the asynchronous operation of storing the dead delivery report.</returns>
    private async Task PersistDeadDeliveryReport(UpdateStatusRetryMessage updateStatusRetryMessage)
    {
        var deadDeliveryReportJson = JsonSerializer.Serialize(
            new
            {
                reason = "RETRY_THRESHOLD_EXCEEDED",
                message = "Retry timeout exceeded",
                originalDeliveryReport = updateStatusRetryMessage.SendOperationResult
            },
            JsonSerializerOptionsProvider.Options);

        var deadDeliveryReport = new DeadDeliveryReport
        {
            Resolved = false,
            Channel = Channel,
            FirstSeen = updateStatusRetryMessage.FirstSeen,
            AttemptCount = updateStatusRetryMessage.Attempts,
            LastAttempt = updateStatusRetryMessage.LastAttempt,
            DeliveryReport = deadDeliveryReportJson
        };

        await _deadDeliveryReportService.InsertAsync(deadDeliveryReport);
    }

    /// <summary>
    /// Executes the notification status update operation with built-in retry capability.
    /// </summary>
    /// <param name="updateStatusRetryMessage">The message containing retry information and delivery report data.</param>
    /// <returns>A task representing the asynchronous status update operation.</returns>
    private async Task ProcessStatusUpdateWithRetry(UpdateStatusRetryMessage updateStatusRetryMessage)
    {
        try
        {
            await UpdateStatusAsync(updateStatusRetryMessage);
        }
        catch (NotificationExpiredException ex)
        {
            // Notification has expired - save to dead delivery reports immediately, don't retry
            _logger.LogInformation(
                ex,
                "// {Class} // ProcessStatusUpdateWithRetry // {Message}",
                GetType().Name,
                ex.Message);

            await SaveDeadDeliveryReportForExpired(updateStatusRetryMessage);
        }
        catch (Exception ex)
        {
            // Other exceptions - continue retrying
            _logger.LogWarning(ex, "// {Class} // ProcessStatusUpdateWithRetry // Update failed. Attempts={Attempts}", GetType().Name, updateStatusRetryMessage.Attempts);

            var incrementedRetryMessage = updateStatusRetryMessage with { Attempts = updateStatusRetryMessage.Attempts + 1, LastAttempt = DateTime.UtcNow };
            await RetryStatus(incrementedRetryMessage.Serialize());
        }
    }

    /// <summary>
    /// Saves a dead delivery report for a notification that has expired.
    /// </summary>
    /// <param name="retryMessage">The retry message containing delivery report information.</param>
    /// <returns>A task representing the asynchronous operation of storing the dead delivery report.</returns>
    private async Task SaveDeadDeliveryReportForExpired(UpdateStatusRetryMessage retryMessage)
    {
        var deadDeliveryReportJson = JsonSerializer.Serialize(
            new
            {
                reason = "NOTIFICATION_EXPIRED",
                message = "Notification expiry time has passed",
                originalDeliveryReport = retryMessage.SendOperationResult
            },
            JsonSerializerOptionsProvider.Options);

        var deadDeliveryReport = new DeadDeliveryReport
        {
            Channel = Channel,
            FirstSeen = retryMessage.FirstSeen,
            LastAttempt = DateTime.UtcNow,
            AttemptCount = retryMessage.Attempts,
            Resolved = false,
            DeliveryReport = deadDeliveryReportJson
        };

        await _deadDeliveryReportService.InsertAsync(deadDeliveryReport);
    }
}
