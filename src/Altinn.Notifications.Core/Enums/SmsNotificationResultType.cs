namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Enum describing sms notification result types
/// </summary>
public enum SmsNotificationResultType
{
    /// <summary>
    /// Default result for new notifications
    /// </summary>
    New,

    /// <summary>
    /// Sms notification being sent
    /// </summary>
    Sending,

    /// <summary>
    /// Sms notification sent to service provider
    /// </summary>
    Accepted,

    /// <summary>
    /// Failed, unknown reason
    /// </summary>
    Failed,

    /// <summary>
    /// Failed, invalid mobilenumber
    /// </summary>
    Failed_InvalidRecipient,

    /// <summary>
    /// Failed, invalid mobilenumber
    /// </summary>
    Failed_RecipientNotIdentified
}
