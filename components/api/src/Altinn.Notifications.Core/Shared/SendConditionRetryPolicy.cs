namespace Altinn.Notifications.Core.Shared;

/// <summary>
/// Defines how inconclusive send condition checks are retried at send time.
/// </summary>
/// <remarks>
/// An inconclusive check (timeout, server error, token failure) is retried once after <see cref="Delay"/>.
/// If the check is still inconclusive, the notification is sent as if no condition endpoint was configured.
/// </remarks>
public static class SendConditionRetryPolicy
{
    /// <summary>
    /// The delay before a notification with an inconclusive send condition check can be claimed again.
    /// </summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMinutes(1);
}
