using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

/// <summary>
/// Wolverine handler for <see cref="SendComposedEmailCommand"/> messages received from Azure Service Bus.
/// </summary>
public static class SendComposedEmailCommandHandler
{
    /// <summary>
    /// Handles a <see cref="SendComposedEmailCommand"/> by mapping it to a <see cref="ComposedEmail"/>
    /// and delegating sending to <see cref="ISendingService.SendComposedAsync"/>.
    /// </summary>
    /// <param name="command">The composed send-email command to process.</param>
    /// <param name="sendingService">The service responsible for downloading attachments and sending the email.</param>
    /// <param name="logger">The logger used to record processing events.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    public static async Task HandleAsync(SendComposedEmailCommand command, ISendingService sendingService, ILogger logger, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EmailContentType>(command.ContentType, ignoreCase: true, out var contentType))
        {
            logger.LogError(
                "SendComposedEmailCommandHandler unknown ContentType for NotificationId {NotificationId}. Defaulting to Plain.",
                command.NotificationId);

            contentType = EmailContentType.Plain;
        }

        logger.LogInformation(
            "Processing SendComposedEmailCommand for NotificationId: {NotificationId}",
            command.NotificationId);

        var email = new ComposedEmail(
            command.NotificationId,
            command.Subject,
            command.Body,
            command.FromAddress,
            command.ToAddress,
            contentType,
            command.Attachments
                .Select(a => new SasFileAttachmentReference
                {
                    Filename = a.Filename,
                    MimeType = a.MimeType,
                    SasUrl = a.SasUrl
                })
                .ToArray());

        try
        {
            await sendingService.SendComposedAsync(email, cancellationToken);

            logger.LogInformation(
                "Successfully dispatched composed email for NotificationId: {NotificationId}",
                command.NotificationId);
        }
        catch (Exception)
        {
            LogOnSendEmailFailed(logger, command.NotificationId);

            throw;
        }
    }

    /// <summary>
    /// Logs a send-email failure at warning level.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="notificationId">The notification ID associated with the failed send attempt.</param>
    private static void LogOnSendEmailFailed(ILogger logger, Guid notificationId)
    {
        logger.LogWarning(
            "SendComposedEmailCommandHandler failed to send email for NotificationId: {NotificationId}.",
            notificationId);
    }
}
