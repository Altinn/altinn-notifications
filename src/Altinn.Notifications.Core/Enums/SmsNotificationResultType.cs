namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Enum representing the result types for SMS notifications.
/// </summary>
public enum SmsNotificationResultType
{
    /// <summary>
    /// The default result for new notifications.
    /// </summary>
    New,

    /// <summary>
    /// The SMS notification is currently being sent.
    /// </summary>
    Sending,

    /// <summary>
    /// The SMS notification has been sent to the service provider.
    /// </summary>
    Accepted,

    /// <summary>
    /// The SMS notification was successfully delivered to the recipient.
    /// </summary>
    Delivered,

    /// <summary>
    /// The SMS notification send operation failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The SMS notification send operation failed due to an invalid recipient.
    /// </summary>
    Failed_InvalidRecipient,

    /// <summary>
    /// The SMS notification send operation failed because the recipient is reserved in KRR.
    /// </summary>
    Failed_RecipientReserved,

    /// <summary>
    /// The SMS notification send operation failed because the recipient's number is barred, blocked, or not in use.
    /// </summary>
    Failed_BarredReceiver,

    /// <summary>
    /// The SMS notification send operation failed because the message has been deleted.
    /// </summary>
    Failed_Deleted,

    /// <summary>
    /// The SMS notification send operation failed because the message validity period has expired.
    /// </summary>
    Failed_Expired,

    /// <summary>
    /// The SMS notification send operation failed because the SMS was undeliverable.
    /// </summary>
    Failed_Undelivered,

    /// <summary>
    /// The SMS notification send operation failed because the recipient's mobile number was not identified.
    /// </summary>
    Failed_RecipientNotIdentified,

    /// <summary>
    /// The SMS notification send operation failed because the message was rejected.
    /// </summary>
    Failed_Rejected
}
