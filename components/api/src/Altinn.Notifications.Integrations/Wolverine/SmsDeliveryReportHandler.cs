using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;

using Npgsql;

using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Handles SMS delivery status updates received from the Azure Service Bus queue.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SmsDeliveryReportHandler
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
    /// Configures error handling for the SMS delivery report queue handler.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        var policy = Settings.SmsDeliveryReportQueuePolicy;

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
            .Then.SaveDeadDeliveryReport(RetryExceededReason, Core.Enums.DeliveryReportChannel.LinkMobility);

        chain
            .OnException<NotificationExpiredException>()
            .SaveDeadDeliveryReport(NotificationExpiredReason, Core.Enums.DeliveryReportChannel.LinkMobility);
    }

    /// <summary>
    /// Handles an SMS delivery report command by updating the notification send status.
    /// </summary>
    public static async Task Handle(
        SmsDeliveryReportCommand command,
        ISmsNotificationService smsNotificationService,
        ILogger logger)
    {
        logger.LogInformation(
            "Received SMS delivery report for GatewayReference: {GatewayReference}, Result: {SendResult}",
            command.GatewayReference,
            command.SendResult);

        var operationResult = new SmsSendOperationResult
        {
            GatewayReference = command.GatewayReference,
            NotificationId = command.NotificationId,
            SendResult = Enum.Parse<Core.Enums.SmsNotificationResultType>(command.SendResult)
        };

        await smsNotificationService.UpdateSendStatus(operationResult);
    }
}
