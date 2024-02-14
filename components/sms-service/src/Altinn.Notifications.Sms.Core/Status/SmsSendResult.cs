namespace Altinn.Notifications.Sms.Core.Status;

/// <summary>
/// Enum describing sms send result types
/// </summary>
public enum SmsSendResult
{
    /// <summary>
    /// Sms send operation running
    /// </summary>
    Sending,

    /// <summary>
    /// Sms send operation accepted
    /// </summary>
    Accepted,

    /// <summary>
    /// Message was successfully delivered to destination.
    /// </summary>
    Delivered,

    /// <summary>
    /// Sms send operation failed
    /// </summary>
    Failed,

    /// <summary>
    /// The receiver number is barred/blocked/not in use. Do not retry message, and remove number from any subscriber list.
    /// </summary>
    Failed_BarredReceiver,

    /// <summary>
    /// Message has been deleted.
    /// </summary>
    Failed_Deleted,

    /// <summary>
    /// Message validity period has expired.
    /// </summary>
    Failed_Expired,

    /// <summary>
    /// Sms send operation failed due to invalid receiver
    /// </summary>
    Failed_InvalidReceiver,

    /// <summary>
    /// The SMS was undeliverable (not a valid number or no available route to destination).
    /// </summary>
    Failed_Undelivered,

    /// <summary>
    /// Message was rejected.
    /// </summary>
    Failed_Rejected,
}
