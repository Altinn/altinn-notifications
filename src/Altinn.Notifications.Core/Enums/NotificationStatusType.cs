namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Represents the status of an order, E-mail notification, and SMS notification in the system.
/// </summary>
public enum NotificationStatusType
{
    /// <summary>
    /// The order has been received and registered in the system but processing has not yet begun.
    /// </summary>
    Order_Registered,

    /// <summary>
    /// The system is actively processing the order and preparing notifications for delivery.
    /// </summary>
    Order_Processing,

    /// <summary>
    /// All processing for the order has been completed, regardless of individual notification outcomes.
    /// </summary>
    Order_Completed,

    /// <summary>
    /// The order was not processed because predefined conditions for sending were not satisfied.
    /// </summary>
    Order_SendConditionNotMet,

    /// <summary>
    /// The order was cancelled before or during processing, either manually or by system rules.
    /// </summary>
    Order_Cancelled,

    /// <summary>
    /// A new SMS notification has been created but not yet submitted for sending.
    /// </summary>
    SMS_New,

    /// <summary>
    /// The SMS notification has been submitted and is currently in the process of being sent.
    /// </summary>
    SMS_Sending,

    /// <summary>
    /// The SMS notification has been accepted by the service provider for delivery to the recipient.
    /// </summary>
    SMS_Accepted,

    /// <summary>
    /// The SMS notification was successfully delivered to the recipient's device.
    /// </summary>
    SMS_Delivered,

    /// <summary>
    /// The SMS notification failed to send for an unspecified reason.
    /// </summary>
    SMS_Failed,

    /// <summary>
    /// The SMS notification failed because the recipient's phone number was invalid or improperly formatted.
    /// </summary>
    SMS_Failed_InvalidRecipient,

    /// <summary>
    /// The SMS notification was not sent because the recipient has reserved against receiving messages according to the KRR register.
    /// </summary>
    SMS_Failed_RecipientReserved,

    /// <summary>
    /// The SMS notification failed because the recipient's number is barred, blocked, or no longer in use.
    /// </summary>
    SMS_Failed_BarredReceiver,

    /// <summary>
    /// The SMS notification was deleted by the system or service provider before reaching the recipient.
    /// </summary>
    SMS_Failed_Deleted,

    /// <summary>
    /// The SMS notification could not be delivered because its validity period expired before delivery was completed.
    /// </summary>
    SMS_Failed_Expired,

    /// <summary>
    /// The SMS notification could not be delivered after multiple delivery attempts.
    /// </summary>
    SMS_Failed_Undelivered,

    /// <summary>
    /// The SMS notification failed because the system could not determine a valid mobile number for the recipient.
    /// </summary>
    SMS_Failed_RecipientNotIdentified,

    /// <summary>
    /// The SMS notification was rejected by the service provider, carrier, or recipient's device.
    /// </summary>
    SMS_Failed_Rejected,

    /// <summary>
    /// A new email notification has been created but not yet submitted for sending.
    /// </summary>
    Email_New,

    /// <summary>
    /// The email notification has been submitted and is currently in the process of being sent.
    /// </summary>
    Email_Sending,

    /// <summary>
    /// The email notification has been successfully sent to the email service provider.
    /// </summary>
    Email_Succeeded,

    /// <summary>
    /// The email notification was successfully delivered to the recipient's inbox.
    /// </summary>
    Email_Delivered,

    /// <summary>
    /// The email notification failed to send for an unspecified reason.
    /// </summary>
    Email_Failed,

    /// <summary>
    /// The email notification was not sent because the recipient has reserved against receiving messages according to the KRR register.
    /// </summary>
    Email_Failed_RecipientReserved,

    /// <summary>
    /// The email notification failed because the system could not determine a valid email address for the recipient.
    /// </summary>
    Email_Failed_RecipientNotIdentified,

    /// <summary>
    /// The email notification failed because the recipient's email address was improperly formatted.
    /// </summary>
    Email_Failed_InvalidFormat,

    /// <summary>
    /// The email notification was not sent because the recipient was on a suppression list.
    /// </summary>
    Email_Failed_SupressedRecipient,

    /// <summary>
    /// The email notification encountered a temporary failure that might succeed with a retry attempt.
    /// </summary>
    Email_Failed_TransientError,

    /// <summary>
    /// The email notification was rejected by the recipient's mail server and returned as undeliverable.
    /// </summary>
    Email_Failed_Bounced,

    /// <summary>
    /// The email notification was filtered and classified as spam by the recipient's email system.
    /// </summary>
    Email_Failed_FilteredSpam,

    /// <summary>
    /// The email notification was placed in quarantine by security systems before reaching the recipient.
    /// </summary>
    Email_Failed_Quarantined
}
