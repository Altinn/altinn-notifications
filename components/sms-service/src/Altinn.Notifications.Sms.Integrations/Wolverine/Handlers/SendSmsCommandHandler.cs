using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Sending;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.Wolverine.Handlers;

/// <summary>
/// Wolverine handler for <see cref="SendSmsCommand"/> messages received from Azure Service Bus.
/// </summary>
public static class SendSmsCommandHandler
{
    /// <summary>
    /// Handles a <see cref="SendSmsCommand"/> by mapping it to an <see cref="Sms"/>
    /// and delegating sending to <see cref="ISendingService"/>.
    /// </summary>
    /// <param name="command">The incoming send-sms command.</param>
    /// <param name="sendingService">The service responsible for sending the SMS.</param>
    /// <param name="logger">The logger used to record processing errors.</param>
    public static async Task HandleAsync(
        SendSmsCommand command,
        ISendingService sendingService,
        ILogger logger)
    {
        if (command.NotificationId == Guid.Empty)
        {
            logger.LogError("Received SendSmsCommand with missing NotificationId.");
            throw new InvalidOperationException("Received SendSmsCommand with missing NotificationId.");
        }

        var sms = new Core.Sending.Sms
        {
            Recipient = command.MobileNumber,
            Message = command.Body,
            Sender = command.SenderNumber,
            NotificationId = command.NotificationId
        };

        try
        {
            await sendingService.SendAsync(sms);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogOnSendSmsFailed(logger, ex, command.NotificationId);

            throw;
        }
    }

    private static void LogOnSendSmsFailed(ILogger logger, Exception exception, Guid notificationId)
    {
        logger.LogError(
            exception,
            "SendSmsCommandHandler failed to send SMS for NotificationId: {NotificationId}.",
            notificationId);
    }
}
