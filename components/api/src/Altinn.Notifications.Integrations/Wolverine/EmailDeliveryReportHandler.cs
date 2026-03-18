using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Handles email delivery status updates received from the Azure Service Bus queue.
/// </summary>
[ExcludeFromCodeCoverage]
public static class EmailDeliveryReportHandler
{
    /// <summary>
    /// Gets or sets the Wolverine settings used for configuring error handling policies.
    /// </summary>
    public static WolverineSettings Settings { get; set; } = null!;

    /// <summary>
    /// Gets the reason code that indicates the retry threshold has been exceeded.
    /// </summary>
    public static string RetryExceededReason => "RETRY_THRESHOLD_EXCEEDED";

    /// <summary>
    /// Gets the reason code indicating that a notification has expired.
    /// </summary>
    public static string NotificationExpiredReason => "NOTIFICATION_EXPIRED";

    /// <summary>
    /// Configures error handling for the email delivery report queue handler.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        var policy = Settings.EmailDeliveryReportQueuePolicy;

        chain
            .OnException<InvalidOperationException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .Or<ServiceBusException>()
            .Or<NpgsqlException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();

        chain
            .OnException<NotificationNotFoundException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.SaveDeadDeliveryReport(RetryExceededReason);

        chain
            .OnException<NotificationExpiredException>()
            .SaveDeadDeliveryReport(NotificationExpiredReason);

        // Permanent failures — no retry, move directly to error queue
        // No need to create an explicit policy for this case since the default behavior is to move to the error queue on unhandled exceptions
    }

    /// <summary>
    /// Handles an email delivery report command by logging the received status update.
    /// </summary>
    public static async Task Handle(
        EmailDeliveryReportCommand command,
        IEmailNotificationService emailNotificationService,
        ILogger<EmailDeliveryReportHandler> logger)
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
                        return;
                    }

                    logger.LogInformation(
                        "Received email delivery report for MessageId: {MessageId}, Status: {Status}",
                        deliveryReport.MessageId,
                        deliveryReport.Status);

                    await HandleDeliveryReport(emailNotificationService, deliveryReport);
                    break;
                default:
                    logger.LogWarning("Received unhandled system event type: {EventType}", systemEvent.GetType().Name);
                    break;
            }
        }
        else
        {
            logger.LogWarning("Failed to parse system event data from Event Grid event. Subject: {Subject}, EventType: {EventType}", eventGridEvent.Subject, eventGridEvent.EventType);
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
