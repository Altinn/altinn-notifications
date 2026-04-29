namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines scheduling operations for notification delivery.
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
    /// Determine whether daytime-restricted email messages are allowed to be sent at the current UTC time, based on the configured email sending window.
    /// </summary>
    /// <returns>
    /// <c>true</c> if email messages can be sent now; otherwise, <c>false</c>.
    /// </returns>
    bool CanSendEmailNow();

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
    /// Calculates when a Daytime email notification should expire, based on a given UTC time and the configured email sending window.
    /// </summary>
    /// <param name="referenceUtcDateTime">
    /// The UTC time used as the starting point for calculating the expiry date and time.
    /// </param>
    /// <returns>
    /// The UTC date and time when the email notification will expire.
    /// </returns>
    DateTime GetEmailExpirationDateTime(DateTime referenceUtcDateTime);
}
