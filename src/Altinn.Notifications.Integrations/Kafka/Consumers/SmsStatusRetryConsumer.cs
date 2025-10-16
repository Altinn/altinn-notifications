using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Kafka consumer for processing retry attempts of SMS notification status updates from the dedicated retry topic.
/// </summary>
public sealed class SmsStatusRetryConsumer(
    IKafkaProducer producer,
    ILogger<SmsStatusRetryConsumer> logger,
    IOptions<Configuration.KafkaSettings> settings,
    ISmsNotificationService smsNotificationService,
    IDeadDeliveryReportService deadDeliveryReportService)
    : NotificationStatusRetryConsumerBase(settings.Value.SmsStatusUpdatedRetryTopicName, producer, settings, deadDeliveryReportService, logger)
{
    /// <summary>
    /// Gets the delivery report channel for Link Mobility SMS notifications.
    /// </summary>
    protected override DeliveryReportChannel Channel => DeliveryReportChannel.LinkMobility;

    /// <summary>
    /// Attempts to update the persisted send status for an SMS notification based on the
    /// serialized <see cref="SmsSendOperationResult"/> contained in the supplied retry envelope.
    /// </summary>
    /// <param name="retryMessage">
    /// The retry envelope holding correlation metadata (attempt count and timestamps) and the raw JSON
    /// payload in <see cref="UpdateStatusRetryMessage.SendOperationResult"/> representing an
    /// <see cref="SmsSendOperationResult"/> returned from the underlying SMS provider.
    /// </param>
    protected override async Task UpdateStatusAsync(UpdateStatusRetryMessage retryMessage)
    {
        if (string.IsNullOrWhiteSpace(retryMessage.SendOperationResult))
        {
            logger.LogError("SendOperationResult is null or empty. RetryMessage: {RetryMessage}", Convert.ToString(retryMessage));
            return;
        }

        var smsSendOperationResult = JsonSerializer.Deserialize<SmsSendOperationResult>(retryMessage.SendOperationResult, JsonSerializerOptionsProvider.Options);
        if (smsSendOperationResult == null)
        {
            logger.LogError("SmsSendOperationResult deserialization returned null. SendOperationResult: {SendOperationResult}", retryMessage.SendOperationResult);
            return;
        }

        await smsNotificationService.UpdateSendStatus(smsSendOperationResult);
    }
}
