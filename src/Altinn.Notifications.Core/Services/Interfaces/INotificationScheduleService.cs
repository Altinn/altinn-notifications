namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines scheduling operations for SMS notification delivery.
/// </summary>
public interface INotificationScheduleService
{
    /// <summary>
    /// Determines whether the current local time is within the configured SMS send window.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the current local time falls within the configured SMS send window; otherwise, <c>false</c>.
    /// </returns>
    bool IsWithinSmsSendWindow();

    /// <summary>
    /// Calculates the expiry date and time for an SMS notification based on the current time and the configured send window.
    /// </summary>
    /// <returns>
    /// A <see cref="DateTime"/> value representing when the SMS notification should expire.
    /// </returns>
    DateTime GetSmsExpiryDateTime();
}
