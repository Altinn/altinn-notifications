namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Represents the policies for scheduling the sending time of a message.
/// </summary>
public enum SendingTimePolicy : uint
{
    /// <summary>
    /// The message will be scheduled for sending during daytime on working days.
    /// The messages are sent between 08:00 and 17:00 CET from Monday to Friday.
    /// </summary>
    WorkingDaysDaytime = 1
}
