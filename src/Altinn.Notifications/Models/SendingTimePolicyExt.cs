using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines policies for when a message should be sent.
/// </summary>
[Description("Defines policies for when a message should be sent.")]
[JsonConverter(typeof(JsonStringEnumConverter<SendingTimePolicyExt>))]
public enum SendingTimePolicyExt
{
    /// <summary>
    /// The message will be scheduled for sending only during daytime on working days (Monday to Friday).
    /// Email and SMS are sent between 08:00 and 17:00 CET.
    /// </summary>
    [Description("Schedules the message for delivery during daytime on working days (08:00 - 17:00 CET, Mon-Fri).")]
    WorkingDaysDaytime,

    /// <summary>
    /// The message will be sent as soon as possible within the next available daytime slot (08:00 - 17:00 CET).
    /// </summary>
    [Description("Schedules the message to be sent in the first available slot between 08:00 and 17:00 CET.")]
    WeekDaysDaytime,

    /// <summary>
    /// The message will be sent immediately or as soon as possible after the specified 'not-before' time.
    /// There are no restrictions on time or day.
    /// </summary>
    [Description("Schedules the message for immediate sending or at the specified 'not-before' time without restrictions.")]
    Unrestricted
}
