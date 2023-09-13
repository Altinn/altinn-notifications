namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Enum describing email notification result types
/// </summary>
public enum EmailNotificationResultType
{
    /// <summary>
    /// Default result for new notifications
    /// </summary>
    New,

    /// <summary>
    /// Email notification being sent
    /// </summary>
    Sending,

    /// <summary>
    /// Email notification sent
    /// </summary>
    Succeeded,

    /// <summary>
    /// Email delivered to recipient
    /// </summary>
    Delivered,

    /// <summary>
    /// Recipient to address was not identified
    /// </summary>
    Failed_RecipientNotIdentified
}