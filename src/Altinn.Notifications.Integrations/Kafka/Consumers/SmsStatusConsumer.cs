using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer class for handling status messages about SMS notifications.
/// </summary>
public sealed class SmsStatusConsumer : NotificationStatusConsumerBase<SmsStatusConsumer, SmsSendOperationResult>
{
    private readonly ISmsNotificationService _smsNotificationsService;

    /// <summary>
    /// Gets the delivery report channel for Link Mobility SMS notifications.
    /// </summary>
    protected override DeliveryReportChannel Channel => DeliveryReportChannel.LinkMobility;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsStatusConsumer"/> class.
    /// </summary>
    public SmsStatusConsumer(
        IKafkaProducer producer,
        ILogger<SmsStatusConsumer> logger,
        IOptions<KafkaSettings> kafkaSettings,
        ISmsNotificationService smsNotificationsService,
        IDeadDeliveryReportService deadDeliveryReportService)
        : base(producer, logger, kafkaSettings.Value.SmsStatusUpdatedTopicName, kafkaSettings.Value.SmsStatusUpdatedRetryTopicName, kafkaSettings, deadDeliveryReportService)
    {
        _smsNotificationsService = smsNotificationsService;
    }

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
