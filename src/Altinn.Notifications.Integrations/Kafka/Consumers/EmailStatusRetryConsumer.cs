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
/// Kafka consumer for processing status messages about email notifications.
/// </summary>
public sealed class EmailStatusRetryConsumer(
    IKafkaProducer producer,
    ILogger<EmailStatusRetryConsumer> logger,
    IOptions<Configuration.KafkaSettings> settings,
    IEmailNotificationService emailNotificationService,
    IDeadDeliveryReportService deadDeliveryReportService)
    : NotificationStatusRetryConsumerBase(settings.Value.EmailStatusUpdatedRetryTopicName, producer, settings, deadDeliveryReportService, logger)
{
    /// <summary>
    /// Gets the delivery report channel for Azure Communication Services email notifications.
    /// </summary>
    protected override DeliveryReportChannel Channel => DeliveryReportChannel.AzureCommunicationServices;

    /// <summary>
    /// Updates the email notification status based on the retry message payload.
    /// </summary>
    /// <param name="retryMessage">The message object containing both metadata and send operation result payload</param>
    /// <returns></returns>
    protected override async Task UpdateStatusAsync(UpdateStatusRetryMessage retryMessage)
    {
        var emailSendOperationResult = JsonSerializer.Deserialize<EmailSendOperationResult>(retryMessage.SendOperationResult, JsonSerializerOptionsProvider.Options);
        if (emailSendOperationResult == null)
        {
            logger.LogError("Message deserialization failed. {Message}", Convert.ToString(retryMessage));
            return;
        }

        await emailNotificationService.UpdateSendStatus(emailSendOperationResult);
    }
}
