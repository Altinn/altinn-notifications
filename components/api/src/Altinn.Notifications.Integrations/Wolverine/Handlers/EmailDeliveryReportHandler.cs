using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Telemetry;

using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles email delivery status updates received from the Azure Service Bus queue.
/// </summary>
[ExcludeFromCodeCoverage]
public static class EmailDeliveryReportHandler
{
    /// <summary>
    /// Handles an email delivery report command by updating the notification send status
    /// and emitting a custom delivery report metric.
    /// </summary>
    public static async Task Handle(
        EmailDeliveryReportCommand command,
        IEmailNotificationService emailNotificationService,
        DeliveryReportMetrics metrics,
        ILogger logger)
    {
        var eventGridEvent = EventGridEvent.Parse(command.Message.Body);

        if (eventGridEvent.TryGetSystemEventData(out object systemEvent))
        {
            switch (systemEvent)
            {
                case AcsEmailDeliveryReportReceivedEventData deliveryReport:
                    if (string.IsNullOrWhiteSpace(deliveryReport.MessageId))
                    {
                        logger.LogError("Received delivery report with missing MessageId. Subject: {Subject}", eventGridEvent.Subject);
                        throw new InvalidDeliveryReportException("Received delivery report with missing MessageId");
                    }

                    logger.LogInformation(
                        "Received email delivery report for MessageId: {MessageId}, Status: {Status}",
                        deliveryReport.MessageId,
                        deliveryReport.Status);

                    metrics.RecordEmailDeliveryReport(
                        status: deliveryReport.Status?.ToString(),
                        statusMessage: deliveryReport.DeliveryStatusDetails?.StatusMessage,
                        recipientMailServerHostName: deliveryReport.DeliveryStatusDetails?.RecipientMailServerHostName,
                        sender: deliveryReport.Sender,
                        recipient: deliveryReport.Recipient);

                    await HandleDeliveryReport(emailNotificationService, deliveryReport);
                    break;

                default:
                    logger.LogWarning("Received unhandled system event type: {EventType}", systemEvent.GetType().Name);
                    throw new InvalidDeliveryReportException("Received unhandled system event type in Event Grid event.");
            }
        }
        else
        {
            logger.LogWarning(
                "Failed to parse system event data from Event Grid event. Subject: {Subject}, EventType: {EventType}",
                eventGridEvent.Subject,
                eventGridEvent.EventType);
            throw new InvalidDeliveryReportException("Failed to parse system event data from Event Grid event.");
        }
    }

    private static async Task HandleDeliveryReport(
        IEmailNotificationService emailNotificationService,
        AcsEmailDeliveryReportReceivedEventData deliveryReport)
    {
        var operationResult = new EmailSendOperationResult
        {
            OperationId = deliveryReport.MessageId,
            SendResult = Utils.ParseDeliveryStatus(deliveryReport.Status?.ToString()),
            DeliveryReport = JsonSerializer.Serialize(deliveryReport, JsonSerializerOptionsProvider.Options)
        };

        await emailNotificationService.UpdateSendStatus(operationResult);
    }
}
