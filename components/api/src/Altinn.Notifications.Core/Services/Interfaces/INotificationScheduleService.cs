namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines scheduling operations for SMS notification delivery.
/// </summary>
public interface INotificationScheduleService
{
    /// <summary>
    /// Determine whether SMS messages are allowed to be sent at the current UTC time, based on the configured sending window.
    /// </summary>
    /// <returns>
    /// <c>true</c> if SMS messages can be sent now; otherwise, <c>false</c>.
    /// </returns>
    bool CanSendSmsNow();

    /// <summary>
    /// Calculates when an SMS notification should expire, based on a given UTC time and the configured sending window.
    /// </summary>
    /// <param name="referenceUtcDateTime">
    /// The UTC time used as the starting point for calculating the expiry date and time.
    /// </param>
    /// <returns>
    /// The UTC date and time when the SMS notification will expire.
    /// </returns>
    DateTime GetSmsExpirationDateTime(DateTime referenceUtcDateTime);

    /// <summary>
    /// Calculates the requested send time that should be used for an order with a send condition and an SMS
    /// notification governed by <see cref="Altinn.Notifications.Core.Enums.SendingTimePolicy.Daytime"/>.
    /// </summary>
    /// <param name="requestedSendTime">The UTC requested send time originally supplied for the order.</param>
    /// <returns>
    /// If <paramref name="requestedSendTime"/> falls outside the configured Daytime send window (09:00-17:00 CET/CEST),
    /// the returned value is postponed to the UTC equivalent of the window's start time (09:00 local): the same day
    /// if <paramref name="requestedSendTime"/> falls before the window opens, or the next day if it falls after the
    /// window closes. Otherwise, <paramref name="requestedSendTime"/> is returned unchanged.
    /// </returns>
    /// <remarks>
    /// This exists to avoid a stale send condition evaluation: without this adjustment, an order registered outside
    /// the Daytime window would have its send condition evaluated immediately, but the SMS notification itself would
    /// not be delivered until the Daytime window opens &#8212; a delay during which the condition could become false.
    /// By postponing <c>RequestedSendTime</c> to align with the Daytime window opening, past-due processing (and
    /// therefore send condition evaluation) is deferred to occur close to actual send time instead.
    /// </remarks>
    DateTime GetRequestedSendTimeForDaytimeSendCondition(DateTime requestedSendTime);
}
