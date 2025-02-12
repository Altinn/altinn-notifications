namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Enum representing the result types for SMS notifications.
/// </summary>
public enum SmsNotificationResultType
{
    /// <summary>
    /// Indicates a new SMS notification.
    /// </summary>
    New,

    /// <summary>
    /// Indicates that the SMS is currently being sent.
    /// </summary>
    Sending,

    /// <summary>
    /// Indicates that the SMS has been sent to the service provider.
    /// </summary>
    Accepted,

    /// <summary>
    /// Indicates that the SMS was successfully delivered to the recipient.
    /// </summary>
    Delivered,

    /// <summary>
    /// Indicates that the SMS send operation failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates that the SMS send operation failed due to an invalid recipient.
    /// </summary>
    Failed_InvalidRecipient,

    /// <summary>
    /// Indicates that the SMS send operation failed because the recipient is reserved due to the contact and reservation register (KRR).
    /// </summary>
    Failed_RecipientReserved,

    /// <summary>
    /// Indicates that the SMS send operation failed because the recipient's number is barred, blocked, or not in use.
    /// </summary>
    Failed_BarredReceiver,

    /// <summary>
    /// Indicates that the SMS send operation failed because the message has been deleted.
    /// </summary>
    Failed_Deleted,

    /// <summary>
    /// Indicates that the SMS send operation failed because the message validity period has expired.
    /// </summary>
    Failed_Expired,

    /// <summary>
    /// Indicates that the SMS send operation failed because the SMS was undeliverable.
    /// </summary>
    Failed_Undelivered,

    /// <summary>
    /// Indicates that the SMS send operation failed because the recipient's mobile number was not identified.
    /// </summary>
    Failed_RecipientNotIdentified,

    /// <summary>
    /// Indicates that the SMS send operation failed because the message was rejected.
    /// </summary>
    Failed_Rejected
}
