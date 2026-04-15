using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;

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
    /// Handles an email delivery report command by logging the received status update.
    /// </summary>
    public static async Task Handle(
        EmailDeliveryReportCommand command,
        IEmailNotificationService emailNotificationService,
        ILogger logger)
    {
        var eventGridEvent = EventGridEvent.Parse(command.Message.Body);

        // If the event is a system event, TryGetSystemEventData will return the deserialized system event
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

                    await HandleDeliveryReport(emailNotificationService, deliveryReport);
                    break;
                default:
                    logger.LogWarning("Received unhandled system event type: {EventType}", systemEvent.GetType().Name);
                    throw new InvalidDeliveryReportException("Received unhandled system event type in Event Grid event.");
            }
        }
        else
        {
            logger.LogWarning("Failed to parse system event data from Event Grid event. Subject: {Subject}, EventType: {EventType}", eventGridEvent.Subject, eventGridEvent.EventType);
            throw new InvalidDeliveryReportException("Failed to parse system event data from Event Grid event.");
        }
    }

    private static async Task HandleDeliveryReport(
        IEmailNotificationService emailNotificationService,
        AcsEmailDeliveryReportReceivedEventData deliveryReport)
    {
        var operationResult = new EmailSendOperationResult()
        {
            OperationId = deliveryReport.MessageId,
            SendResult = Utils.ParseDeliveryStatus(deliveryReport.Status?.ToString())
        };

        await emailNotificationService.UpdateSendStatus(operationResult);
    }
}
