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
/// Base class for notification status consumers handling deserialize, status update, and selective retry.
/// </summary>
/// <typeparam name="TConsumer">The type of the consumer.</typeparam>
/// <typeparam name="TResult">The type of the result after deserializing the message.</typeparam>
public abstract class NotificationStatusConsumerBase<TConsumer, TResult> : KafkaConsumerBase<TConsumer>
    where TResult : class
    where TConsumer : class
{
    private readonly IKafkaProducer _producer;
    private readonly ILogger<TConsumer> _logger;
    private readonly string _statusUpdatedTopicName;
    private readonly string _statusUpdatedRetryTopicName;
    private readonly IDeadDeliveryReportService _deadDeliveryReportService;

    /// <summary>
    /// Gets the delivery report channel for this consumer (e.g., Email = AzureCommunicationServices, SMS = LinkMobility).
    /// </summary>
    protected abstract DeliveryReportChannel Channel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationStatusConsumerBase{TConsumer, TResult}"/> class.
    /// </summary>
    protected NotificationStatusConsumerBase(
        IKafkaProducer producer,
        ILogger<TConsumer> logger,
        string statusUpdatedTopicName,
        string statusUpdatedRetryTopicName,
        IOptions<KafkaSettings> kafkaSettings,
        IDeadDeliveryReportService deadDeliveryReportService)
        : base(kafkaSettings, logger, statusUpdatedTopicName)
    {
        _logger = logger;
        _producer = producer;
        _statusUpdatedTopicName = statusUpdatedTopicName;
        _statusUpdatedRetryTopicName = statusUpdatedRetryTopicName;
        _deadDeliveryReportService = deadDeliveryReportService;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeMessage(ProcessStatus, RetryStatus, stoppingToken), stoppingToken);
    }

    /// <summary>
    /// Updates the notification status based on the parsed result.
    /// </summary>
    /// <param name="result">The parsed result containing status update information.</param>
    protected abstract Task UpdateStatusAsync(TResult result);

    /// <summary>
    /// Attempts to parse a message into the result type.
    /// </summary>
    protected abstract bool TryParse(string message, out TResult result);

    /// <summary>
    /// Processes a delivery report message received from Kafka.
    /// </summary>
    /// <param name="message">The raw message to process.</param>
    private async Task ProcessStatus(string message)
    {
        if (!TryParse(message, out TResult result))
        {
            _logger.LogError("// {Consumer} // ProcessStatus // Deserialization of message failed. Skipping. {Message}", typeof(TConsumer).Name, message);
            return;
        }

        try
        {
            await UpdateStatusAsync(result);
        }
        catch (NotificationExpiredException ex)
        {
            // Notification has expired - save to dead delivery reports immediately, don't retry
            _logger.LogInformation(
                ex,
                "// {Consumer} // ProcessStatus // {Message}",
                typeof(TConsumer).Name,
                ex.Message);

            await SaveDeadDeliveryReport(message);
        }
        catch (NotificationNotFoundException)
        {
            // Notification not found - send to retry topic (might arrive later)
            var updateStatusRetryMessage = new UpdateStatusRetryMessage
            {
                Attempts = 1,
                FirstSeen = DateTime.UtcNow,
                LastAttempt = DateTime.UtcNow,
                SendOperationResult = message
            }.Serialize();

            await _producer.ProduceAsync(_statusUpdatedRetryTopicName, updateStatusRetryMessage);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            LogProcessingError(message);
        }
    }

    /// <summary>
    /// Republishes the message to the same status-updated topic.
    /// </summary>
    private async Task RetryStatus(string message)
    {
        var producerResult = await _producer.ProduceAsync(_statusUpdatedTopicName, message);
        if (!producerResult)
        {
            throw new InvalidOperationException($"Failed to republish message to topic. Not retrying on topic: {_statusUpdatedTopicName}");
        }
    }

    /// <summary>
    /// Logs an error when an exception occurs while processing (after successful deserialization).
    /// </summary>
    private void LogProcessingError(string message)
    {
        _logger.LogError("// {Consumer} // ProcessStatus // Failed while applying status update. Not retrying. {Message}", typeof(TConsumer).Name, message);
    }

    /// <summary>
    /// Saves a dead delivery report for a notification that has expired.
    /// </summary>
    private async Task SaveDeadDeliveryReport(string originalMessage)
    {
        var deadDeliveryReport = new DeadDeliveryReport
        {
            Channel = Channel,
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow,
            AttemptCount = 1,
            Resolved = false,
            DeliveryReport = originalMessage,
            Reason = "NOTIFICATION_EXPIRED",
            Message = "Notification expiry time has passed"
        };

        await _deadDeliveryReportService.InsertAsync(deadDeliveryReport);
    }
}
