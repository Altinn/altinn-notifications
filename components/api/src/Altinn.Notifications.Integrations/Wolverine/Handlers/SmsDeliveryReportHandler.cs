using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles SMS delivery status updates received from the Azure Service Bus queue.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SmsDeliveryReportHandler
{
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
