using System;
using System.Linq;

namespace Altinn.Notifications.Shared.Configuration;

/// <summary>
/// Represents retry policy configuration for a queue.
/// </summary>
public class QueueRetryPolicy
{
    /// <summary>
    /// Cooldown retry delays in milliseconds.
    /// These retries happen within the same message lock and are fast retries for transient issues.
    /// </summary>
    public int[] CooldownDelaysMs { get; set; } = [];

    /// <summary>
    /// Scheduled retry delays in milliseconds.
    /// These retries release the message lock and schedule the message for later processing.
    /// Used for longer transient issues or service outages.
    /// </summary>
    public int[] ScheduleDelaysMs { get; set; } = [];

    /// <summary>
    /// Converts cooldown delays from milliseconds to TimeSpan array.
    /// </summary>
    public TimeSpan[] GetCooldownDelays()
        => [.. CooldownDelaysMs.Select(ms => TimeSpan.FromMilliseconds(ms))];

    /// <summary>
    /// Converts schedule delays from milliseconds to TimeSpan array.
    /// </summary>
    public TimeSpan[] GetScheduleDelays()
        => [.. ScheduleDelaysMs.Select(ms => TimeSpan.FromMilliseconds(ms))];
}
