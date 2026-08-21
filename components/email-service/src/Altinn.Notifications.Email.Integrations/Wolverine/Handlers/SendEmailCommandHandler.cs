using System.Diagnostics;

using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

/// <summary>
/// Wolverine handler for <see cref="SendEmailCommand"/> messages received from Azure Service Bus.
/// </summary>
public static class SendEmailCommandHandler
{
    private static int _globalInitCount = 0;
    private static int _globalExitCount = 0;
    private static int _globalErrorInitCount = 0;
    private static int _globalErrorExitCount = 0;

    /// <summary>
    /// Handles a <see cref="SendEmailCommand"/> by mapping it to an <see cref="Core.Sending.Email"/>
    /// and delegating sending to <see cref="ISendingService"/>.
    /// </summary>
    /// <param name="command">The send-email command to process.</param>
    /// <param name="sendingService">The service responsible for sending the email.</param>
    /// <param name="logger">The logger used to record processing errors.</param>
    public static async Task HandleAsync(SendEmailCommand command, ISendingService sendingService, ILogger logger)
    {
        var globalInitCount = Interlocked.Increment(ref _globalInitCount);
        using Activity? activity = Activity.Current?.Source.StartActivity("SendEmailCommandHandler");
        if (!Enum.TryParse<EmailContentType>(command.ContentType, ignoreCase: true, out var contentType))
        {
            logger.LogError(
                "SendEmailCommandHandler unknown ContentType for NotificationId {NotificationId}. Defaulting to Plain.",
                command.NotificationId);

            contentType = EmailContentType.Plain;
        }

        logger.LogInformation(
            "Processing SendEmailCommand for NotificationId: {NotificationId}",
            command.NotificationId);

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

            logger.LogInformation(
                "Successfully dispatched email for NotificationId: {NotificationId}",
                command.NotificationId);

            var globalExitCount = Interlocked.Increment(ref _globalExitCount);
            Activity.Current?.SetTag("Counters", $"{globalInitCount}, {globalExitCount}, {_globalErrorInitCount}, {_globalErrorExitCount}");
        }
        catch (Exception)
        {
            var globalErrorInitCount = Interlocked.Increment(ref _globalErrorInitCount);
            LogOnSendEmailFailed(logger, command.NotificationId);
            var globalErrorExitCount = Interlocked.Increment(ref _globalErrorExitCount);

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
            "SendEmailCommandHandler failed to send email for NotificationId: {NotificationId}.",
            notificationId);
    }
}
