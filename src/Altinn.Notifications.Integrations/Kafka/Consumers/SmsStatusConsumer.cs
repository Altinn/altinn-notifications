using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for handling status messages about SMS notifications.
/// Responsible for consuming messages, updating notification status,
/// retrying failed updates, and managing log suppression for repeated failures.
/// </summary>
public sealed class SmsStatusConsumer : NotificationStatusConsumerBase<SmsStatusConsumer, SmsSendOperationResult>
{
    private readonly ISmsNotificationService _smsNotificationsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsStatusConsumer"/> class.
    /// </summary>
    /// <param name="producer">The Kafka producer used for publishing retry messages.</param>
    /// <param name="settings">Kafka configuration settings.</param>
    /// <param name="logger">Logger for the consumer.</param>
    /// <param name="smsNotificationsService">Service for handling SMS notification operations.</param>
    public SmsStatusConsumer(
        IKafkaProducer producer,
        IOptions<KafkaSettings> settings,
        ILogger<SmsStatusConsumer> logger,
        ISmsNotificationService smsNotificationsService)
        : base(
            settings.Value.SmsStatusUpdatedTopicName, 
            settings.Value.SmsStatusUpdatedTopicName, 
            settings.Value.SmsStatusUpdatedRetryTopicName, 
            producer, 
            settings, 
            logger)
    {
        _smsNotificationsService = smsNotificationsService;
    }

    /// <summary>
    /// Gets the name of the notification channel being processed.
    /// </summary>
    /// <returns>The string "sms" representing the SMS notification channel.</returns>
    protected override string ChannelName => "sms";

    /// <summary>
    /// Attempts to parse a message into a <see cref="SmsSendOperationResult"/> object.
    /// </summary>
    /// <param name="message">The message to parse.</param>
    /// <param name="result">The parsed result if successful; otherwise, a default-initialized instance.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    protected override bool TryParse(string message, out SmsSendOperationResult result) => SmsSendOperationResult.TryParse(message, out result);

    /// <summary>
    /// Updates the SMS notification status based on the parsed result.
    /// </summary>
    /// <param name="result">The parsed result containing SMS status update information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override Task UpdateStatusAsync(SmsSendOperationResult result) => _smsNotificationsService.UpdateSendStatus(result);
}
