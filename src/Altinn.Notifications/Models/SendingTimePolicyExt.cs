namespace Altinn.Notifications.Models;

/// <summary>
/// Represents the policies for scheduling the sending time of a message.
/// </summary>
public enum SendingTimePolicyExt : uint
{
    /// <summary>
    /// The message can be sent at any time.
    /// </summary>
    Anytime = 1,

    /// <summary>
    /// The messages will be scheduled for sending between 08:00 and 17:00 CET.
    /// </summary>
    Daytime = 2
}
