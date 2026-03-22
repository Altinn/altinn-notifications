using Altinn.Notifications.Shared.Commands;

namespace Altinn.Notifications.Sms.Integrations.Wolverine;

/// <summary>
/// Provides functionality to handle commands for sending SMS messages asynchronously.
/// </summary>
/// <remarks>This static class is intended to process SMS sending commands. All members are thread-safe and can be
/// used concurrently. Ensure that the provided command contains valid data before invoking handler methods.</remarks>
public static class SendSmsCommandHandler
{
    /// <summary>
    /// Consume the SendSmsCommand to send an SMS message.
    /// </summary>
    /// <param name="command">The contract associated with this handler</param>
    /// <returns></returns>
    public static async Task Handle(SendSmsCommand command)
    {
        await Task.CompletedTask;
    }
}
