using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Handles terminal email send operation results received from the Azure Service Bus queue.
/// Published by the email service polling loop when <c>EnableEmailSendResultPublisher</c> is enabled.
/// </summary>
public static class EmailSendResultHandler
{
    /// <summary>
    /// Processes a terminal email send operation result by updating the notification status
    /// using existing application services.
    /// </summary>
    public static async Task Handle(
        EmailSendResultCommand command,
        IEmailNotificationService emailNotificationService,
        ILogger logger)
    {
        if (!Enum.TryParse<EmailNotificationResultType>(command.SendResult, ignoreCase: false, out var sendResult))
        {
            logger.LogError(
                "EmailSendResultHandler received unrecognized SendResult value: {SendResult}. NotificationId: {NotificationId}, OperationId: {OperationId}",
                command.SendResult,
                command.NotificationId,
                command.OperationId);

            throw new ArgumentException($"Unrecognized SendResult value: '{command.SendResult}'", nameof(command));
        }

        var operationResult = new EmailSendOperationResult
        {
            SendResult = sendResult,
            NotificationId = command.NotificationId,
            OperationId = command.OperationId ?? string.Empty
        };

        await emailNotificationService.UpdateSendStatus(operationResult);
    }
}
