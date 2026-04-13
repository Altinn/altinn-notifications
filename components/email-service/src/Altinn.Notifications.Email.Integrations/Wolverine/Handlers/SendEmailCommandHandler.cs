using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

/// <summary>
/// Wolverine handler for <see cref="SendEmailCommand"/> messages received from Azure Service Bus.
/// </summary>
public static class SendEmailCommandHandler
{
    /// <summary>
    /// Handles a <see cref="SendEmailCommand"/> by mapping it to an <see cref="Core.Sending.Email"/>
    /// and delegating sending to <see cref="ISendingService"/>.
    /// </summary>
    /// <param name="command">The send-email command to process.</param>
    /// <param name="sendingService">The service responsible for sending the email.</param>
    /// <param name="logger">The logger used to record processing errors.</param>
    public static async Task HandleAsync(SendEmailCommand command, ISendingService sendingService, ILogger logger)
    {
        if (!Enum.TryParse<EmailContentType>(command.ContentType, ignoreCase: true, out var contentType))
        {
            logger.LogError(
                "SendEmailCommandHandler unknown ContentType for NotificationId {NotificationId}. Defaulting to Plain.",
                command.NotificationId);

            contentType = EmailContentType.Plain;
        }

        var email = new Core.Sending.Email(
            command.NotificationId,
            command.Subject,
            command.Body,
            command.FromAddress,
            command.ToAddress,
            contentType);

        try
        {
            await sendingService.SendAsync(email);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogOnSendEmailFailed(logger, ex, command.NotificationId);

            throw;
        }
    }

    /// <summary>
    /// Logs a send-email failure at error level.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="notificationId">The notification ID associated with the failed send attempt.</param>
    private static void LogOnSendEmailFailed(ILogger logger, Exception exception, Guid notificationId)
    {
        logger.LogError(
            exception,
            "SendEmailCommandHandler failed to send email for NotificationId: {NotificationId}.",
            notificationId);
    }
}
