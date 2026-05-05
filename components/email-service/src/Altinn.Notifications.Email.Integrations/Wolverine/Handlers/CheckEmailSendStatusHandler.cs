using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

using Wolverine;
using Wolverine.Attributes;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Handlers;

/// <summary>
/// Handles <see cref="CheckEmailSendStatusCommand"/> by polling ACS for email delivery status.
/// </summary>
[WolverineHandler]
public static class CheckEmailSendStatusHandler
{
    private const int _statusPollDelayMs = 8000;

    /// <summary>
    /// Polls ACS for delivery status. If the result is terminal, dispatches the result
    /// via <see cref="IEmailSendResultDispatcher"/> (Kafka or ASB depending on configuration)
    /// so the API can update the notification status. If still sending, re-schedules the command
    /// on ASB with an 8-second delay so the polling loop continues.
    /// </summary>
    public static async Task Handle(
        CheckEmailSendStatusCommand checkEmailSendStatusCommand,
        IDateTimeService dateTime,
        IMessageContext messageContext,
        IEmailServiceClient emailService,
        IEmailSendResultDispatcher sendResultDispatcher)
    {
        if (checkEmailSendStatusCommand.NotificationId == Guid.Empty)
        {
            throw new ArgumentException("NotificationId cannot be empty.", nameof(checkEmailSendStatusCommand));
        }

        if (string.IsNullOrWhiteSpace(checkEmailSendStatusCommand.SendOperationId))
        {
            throw new ArgumentException("SendOperationId cannot be null, empty, or whitespace.", nameof(checkEmailSendStatusCommand));
        }

        EmailSendResult sendResult = await emailService.GetOperationUpdate(checkEmailSendStatusCommand.SendOperationId);

        if (sendResult != EmailSendResult.Sending)
        {
            var operationResult = new SendOperationResult
            {
                SendResult = sendResult,
                OperationId = checkEmailSendStatusCommand.SendOperationId,
                NotificationId = checkEmailSendStatusCommand.NotificationId
            };

            await sendResultDispatcher.DispatchAsync(operationResult);
        }
        else
        {
            var recheckEmailSendStatusCommand = new CheckEmailSendStatusCommand
            {
                LastCheckedAtUtc = dateTime.UtcNow(),
                NotificationId = checkEmailSendStatusCommand.NotificationId,
                SendOperationId = checkEmailSendStatusCommand.SendOperationId
            };

            await messageContext.ScheduleAsync(recheckEmailSendStatusCommand, TimeSpan.FromMilliseconds(_statusPollDelayMs));
        }
    }
}
