using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Status;

/// <summary>
/// Represents the lifecycle status of orders and individual notifications.
/// </summary>
/// <remarks>
/// This enum categorizes the different states that an order, SMS notification, or email notification
/// can transition through during its lifecycle - from registration to completion or failure.
/// 
/// The status values are prefixed with their respective domain (Order_, SMS_, or Email_) for clarity.
/// Values are explicitly assigned in distinct ranges:
/// - Order statuses: 0-24
/// - SMS statuses: 25-49
/// - Email statuses: 50-74
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcessingLifecycleExt
{
    /// <summary>
    /// The order has been received and registered in the system but processing has not yet begun.
    /// </summary>
    /// <remarks>
    /// This is the initial state for all notification orders upon submission.
    /// </remarks>
    Order_Registered = 0,

    /// <summary>
    /// The system is actively processing the order and preparing email and/or SMS notifications for delivery.
    /// </summary>
    /// <remarks>
    /// During this state, the system is validating recipients, generating email and/or SMS notifications, 
    /// and preparing them for delivery through the appropriate channels.
    /// </remarks>
    Order_Processing = 1,

    /// <summary>
    /// The order has been successfully completed and all notifications have reached their final status.
    /// </summary>
    /// <remarks>
    /// This is the ultimate terminal state for an order, indicating that:
    /// 1. All processing activities have been completed
    /// 2. All notifications have received their final status reports from service providers
    /// 3. No further status changes are expected
    /// 
    /// This differs from Order_Processed, where notifications have been generated but are still awaiting
    /// final delivery status reports.
    /// </remarks>
    Order_Completed = 2,

    /// <summary>
    /// The order was not processed because predefined conditions for sending were not satisfied.
    /// </summary>
    /// <remarks>
    /// This occurs when an order has conditional sending requirements that weren't met,
    /// such as when a condition endpoint returned a negative result.
    /// </remarks>  
    Order_SendConditionNotMet = 3,

    /// <summary>
    /// The order was cancelled before or during processing, either manually or by system rules.
    /// </summary>
    /// <remarks>
    /// Cancellation can be triggered by administrative action or automated system rules
    /// like exceeding retry limits or detection of system-wide issues.
    /// </remarks>
    Order_Cancelled = 4,

    /// <summary>
    /// The order has been processed, but its final delivery status is pending.
    /// </summary>
    /// <remarks>
    /// In this state, the system has successfully processed the order and generated all required notifications,
    /// but is still awaiting final delivery status reports from SMS or email service providers.
    /// This differs from Order_Completed, which indicates all notifications have received their final status.
    /// </remarks>
    Order_Processed = 5,

    /// <summary>
    /// The SMS notification has been received and registered in the system but processing has not yet begun.
    /// </summary>
    /// <remarks>
    /// This is the initial state for all SMS notifications upon processing the order completely.
    /// </remarks>
    SMS_New = 25,

    /// <summary>
    /// The SMS notification has been submitted and is currently in the process of being sent.
    /// </summary>
    /// <remarks>
    /// The notification has been passed to the SMS service provider and is awaiting delivery confirmation.
    /// </remarks>
    SMS_Sending = 26,

    /// <summary>
    /// The SMS notification has been accepted by the service provider for delivery to the recipient.
    /// </summary>
    /// <remarks>
    /// The service provider has confirmed that the message is valid and will attempt delivery.
    /// </remarks>
    SMS_Accepted = 27,

    /// <summary>
    /// The SMS notification was successfully delivered to the recipient's device.
    /// </summary>
    /// <remarks>
    /// A delivery confirmation has been received from the service provider, indicating successful receipt by the recipient's device.
    /// </remarks>
    SMS_Delivered = 28,

    /// <summary>
    /// The SMS notification failed to send for an unspecified reason.
    /// </summary>
    /// <remarks>
    /// This is a general failure state when the specific reason for failure isn't known or doesn't match other defined failure types.
    /// </remarks>
    SMS_Failed = 29,

    /// <summary>
    /// The SMS notification failed because the recipient's phone number was invalid or improperly formatted.
    /// </summary>
    /// <remarks>
    /// The provided phone number doesn't conform to required formats or is recognized as invalid by the carrier.
    /// </remarks>
    SMS_Failed_InvalidRecipient = 30,

    /// <summary>
    /// The SMS notification was not sent because the recipient has reserved against receiving messages according to the KRR register.
    /// </summary>
    /// <remarks>
    /// The recipient has explicitly opted out of receiving notifications through the Norwegian Contact and Reservation Register (KRR).
    /// </remarks>
    SMS_Failed_RecipientReserved = 31,

    /// <summary>
    /// The SMS notification failed because the recipient's number is barred, blocked, or no longer in use.
    /// </summary>
    /// <remarks>
    /// The carrier or service provider has indicated that the number cannot receive messages.
    /// </remarks>
    SMS_Failed_BarredReceiver = 32,

    /// <summary>
    /// The SMS notification was deleted by the system or service provider before reaching the recipient.
    /// </summary>
    /// <remarks>
    /// The message was removed from the delivery queue by the system or service provider.
    /// </remarks>
    SMS_Failed_Deleted = 33,

    /// <summary>
    /// The SMS notification could not be delivered because its validity period expired before delivery was completed.
    /// </summary>
    /// <remarks>
    /// The message's time-to-live was exceeded before successful delivery.
    /// </remarks>
    SMS_Failed_Expired = 34,

    /// <summary>
    /// The SMS notification could not be delivered after multiple delivery attempts.
    /// </summary>
    /// <remarks>
    /// The service provider made all configured retry attempts without successfully delivering the message.
    /// </remarks>
    SMS_Failed_Undelivered = 35,

    /// <summary>
    /// The SMS notification failed because the system could not determine a valid mobile number for the recipient.
    /// </summary>
    /// <remarks>
    /// No mobile number could be found for the recipient in the available contact registries.
    /// </remarks>
    SMS_Failed_RecipientNotIdentified = 36,

    /// <summary>
    /// The SMS notification was rejected by the service provider, carrier, or recipient's device.
    /// </summary>
    /// <remarks>
    /// The message was explicitly rejected during the delivery process.
    /// </remarks>
    SMS_Failed_Rejected = 37,

    /// <summary>
    /// The email notification has been received and registered in the system but processing has not yet begun.
    /// </summary>
    /// <remarks>
    /// This is the initial state for all email notifications upon processing the order completely.
    /// </remarks>
    Email_New = 50,

    /// <summary>
    /// The email notification has been submitted and is currently in the process of being sent.
    /// </summary>
    /// <remarks>
    /// The notification has been passed to the email service provider and is awaiting delivery confirmation.
    /// </remarks>
    Email_Sending = 51,

    /// <summary>
    /// The email notification has been successfully sent to the email service provider.
    /// </summary>
    /// <remarks>
    /// The service provider has accepted the email for delivery, though final delivery to the recipient's inbox is not yet confirmed.
    /// </remarks>
    Email_Succeeded = 52,

    /// <summary>
    /// The email notification was successfully delivered to the recipient's inbox.
    /// </summary>
    /// <remarks>
    /// A delivery confirmation has been received, indicating the email has reached the recipient's inbox.
    /// </remarks>
    Email_Delivered = 53,

    /// <summary>
    /// The email notification failed to send for an unspecified reason.
    /// </summary>
    /// <remarks>
    /// This is a general failure state when the specific reason for failure isn't known or doesn't match other defined failure types.
    /// </remarks>
    Email_Failed = 54,

    /// <summary>
    /// The email notification was not sent because the recipient has reserved against receiving messages according to the KRR register.
    /// </summary>
    /// <remarks>
    /// The recipient has explicitly opted out of receiving notifications through the Norwegian Contact and Reservation Register (KRR).
    /// </remarks>
    Email_Failed_RecipientReserved = 55,

    /// <summary>
    /// The email notification failed because the system could not determine a valid email address for the recipient.
    /// </summary>
    /// <remarks>
    /// No email address could be found for the recipient in the available contact registries.
    /// </remarks>
    Email_Failed_RecipientNotIdentified = 56,

    /// <summary>
    /// The email notification failed because the recipient's email address was improperly formatted.
    /// </summary>
    /// <remarks>
    /// The provided email address doesn't conform to required formats or contains syntax errors.
    /// </remarks>
    Email_Failed_InvalidFormat = 57,

    /// <summary>
    /// The email notification was not sent because the recipient was on a suppression list.
    /// </summary>
    /// <remarks>
    /// The recipient's address is on a do-not-send list maintained by the service provider.
    /// </remarks>
    Email_Failed_SuppressedRecipient = 58,

    /// <summary>
    /// The email notification encountered a temporary failure that might succeed with a retry attempt.
    /// </summary>
    /// <remarks>
    /// The service provider encountered a temporary issue, and the message might be delivered successfully on retry.
    /// </remarks>
    Email_Failed_TransientError = 59,

    /// <summary>
    /// The email notification was rejected by the recipient's mail server and returned as undeliverable.
    /// </summary>
    /// <remarks>
    /// A hard bounce occurred, indicating permanent delivery failure.
    /// </remarks>
    Email_Failed_Bounced = 60,

    /// <summary>
    /// The email notification was filtered and classified as spam by the recipient's email system.
    /// </summary>
    /// <remarks>
    /// The message was delivered but placed in the spam/junk folder or rejected as spam.
    /// </remarks>
    Email_Failed_FilteredSpam = 61,

    /// <summary>
    /// The email notification was placed in quarantine by security systems before reaching the recipient.
    /// </summary>
    /// <remarks>
    /// The message was flagged by security systems for manual review before potential delivery.
    /// </remarks>
    Email_Failed_Quarantined = 62
}
