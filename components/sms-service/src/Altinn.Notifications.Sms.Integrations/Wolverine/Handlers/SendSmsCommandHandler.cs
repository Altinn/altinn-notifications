using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Sending;

using Wolverine.Attributes;

namespace Altinn.Notifications.Sms.Integrations.Wolverine.Handlers;

/// <summary>
/// Provides functionality to handle commands for sending SMS messages asynchronously.
/// </summary>
/// <remarks>This static class is intended to process SMS sending commands. All members are thread-safe and can be
/// used concurrently. Ensure that the provided command contains valid data before invoking handler methods.</remarks>
[WolverineHandler]
public static class SendSmsCommandHandler
{
    /// <summary>
    /// Handles a <see cref="SendSmsCommand"/> by converting it to an <see cref="Sms"/>
    /// and forwarding it to the sending service.
    /// </summary>
    /// <param name="command">The incoming send-sms command.</param>
    /// <param name="sendingService">The sms sending service.</param>
    public static async Task HandleAsync(
        SendSmsCommand command,
        ISendingService sendingService)
    {
        var sms = new Core.Sending.Sms
        {
            Recipient = command.MobileNumber,
            Message = command.Body,
            Sender = command.SenderNumber,
            NotificationId = command.NotificationId
        };
        
        await sendingService.SendAsync(sms);
    }
}
