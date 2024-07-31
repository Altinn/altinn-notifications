namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Enum describing available notification channels.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// The selected channel for the notification is email.
    /// </summary>
    Email,

    /// <summary>
    /// The selected channel for the notification is SMS.
    /// </summary>
    Sms,

    /// <summary>
    /// The selected channel for the notification is email and to use SMS if email is not available.
    /// </summary>
    EmailPreferred,

    /// <summary>
    /// The selected channel for the notification is SMS and to use email if SMS is not available.
    /// </summary>
    SmsPreferred
}
