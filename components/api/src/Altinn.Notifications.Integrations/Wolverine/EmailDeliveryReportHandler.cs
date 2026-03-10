using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Shared.Configuration;

using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
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
    /// When true, the handler throws to simulate a failure.
    /// Use in development to test retry and dead-letter behavior.
    /// </summary>
    public static bool SimulateFailure { get; set; } = false;

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
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// Handles an email delivery report command by logging the received status update.
    /// </summary>
    public static async Task Handle(EmailDeliveryReportCommand command, IEmailNotificationService emailNotificationService, ILogger logger)
    {
        if (SimulateFailure)
        {
            throw new InvalidOperationException("Simulated failure for testing purposes.");
        }

        var eventGridEvent = EventGridEvent.Parse(command.Message.Body);

        // If the event is a system event, TryGetSystemEventData will return the deserialized system event
        if (eventGridEvent.TryGetSystemEventData(out object systemEvent))
        {
            switch (systemEvent)
            {
                case AcsEmailDeliveryReportReceivedEventData deliveryReport:
                    var operationResult = new EmailSendOperationResult()
                    {
                        OperationId = deliveryReport.MessageId,
                        SendResult = Utils.ParseDeliveryStatus(deliveryReport.Status?.ToString())
                    };
                    await emailNotificationService.UpdateSendStatus(operationResult);
                    break;
            }
        }
    }
}
