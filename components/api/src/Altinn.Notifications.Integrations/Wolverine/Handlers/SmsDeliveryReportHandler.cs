using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Exceptions;
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
        if (string.IsNullOrWhiteSpace(command.GatewayReference))
        {
            logger.LogError("Received SMS delivery report with missing GatewayReference.");
            throw new InvalidDeliveryReportException("Received SMS delivery report with missing GatewayReference.");
        }

        if (!Enum.TryParse<Core.Enums.SmsNotificationResultType>(command.SendResult, out var sendResult))
        {
            logger.LogError("Received SMS delivery report with unrecognized SendResult: {SendResult}", command.SendResult);
            throw new InvalidDeliveryReportException($"Received SMS delivery report with unrecognized SendResult: '{command.SendResult}'.");
        }

        logger.LogInformation(
            "Received SMS delivery report for GatewayReference: {GatewayReference}, Result: {SendResult}",
            command.GatewayReference,
            command.SendResult);

        var operationResult = new SmsSendOperationResult
        {
            GatewayReference = command.GatewayReference,
            NotificationId = command.NotificationId,
            SendResult = sendResult
        };

        await smsNotificationService.UpdateSendStatus(operationResult);
    }
}
