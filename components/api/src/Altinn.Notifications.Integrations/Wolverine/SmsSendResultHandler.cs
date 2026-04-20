using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Handles terminal SMS send operation results received from the Azure Service Bus queue.
/// Published by the SMS service when <c>EnableSmsSendResultPublisher</c> is enabled.
/// </summary>
public static class SmsSendResultHandler
{
    /// <summary>
    /// Processes a terminal SMS send operation result by updating the notification status
    /// using existing application services.
    /// </summary>
    public static async Task Handle(
        SmsSendResultCommand command,
        ISmsNotificationService smsNotificationService,
        ILogger logger)
    {
        if (!Enum.TryParse<SmsNotificationResultType>(command.SendResult, ignoreCase: false, out var sendResult))
        {
            logger.LogError(
                "SmsSendResultHandler received unrecognized SendResult value: {SendResult}. NotificationId: {NotificationId}, GatewayReference: {GatewayReference}",
                command.SendResult,
                command.NotificationId,
                command.GatewayReference);

            throw new ArgumentException($"Unrecognized SendResult value: '{command.SendResult}'", nameof(command));
        }

        var operationResult = new SmsSendOperationResult
        {
            SendResult = sendResult,
            NotificationId = command.NotificationId,
            GatewayReference = command.GatewayReference ?? string.Empty
        };

        await smsNotificationService.UpdateSendStatus(operationResult);
    }
}
