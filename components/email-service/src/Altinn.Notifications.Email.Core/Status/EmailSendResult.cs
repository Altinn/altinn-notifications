namespace Altinn.Notifications.Email.Core.Status;

/// <summary>
/// Enum describing email send result types
/// </summary>
public enum EmailSendResult
{
    /// <summary>
    /// Email send operation running
    /// </summary>
    Sending,

    /// <summary>
    /// Email send operation succeeded
    /// </summary>
    Succeeded,

    /// <summary>
    /// Email delivered to recipient
    /// </summary>
    Delivered,

    /// <summary>
    /// Failed, unknown reason
    /// </summary>
    Failed,

    /// <summary>
    /// Invalid format for email address
    /// </summary>
    Failed_InvalidEmailFormat,

    /// <summary>
    /// Failed, recipient was suppressed
    /// </summary>
    Failed_SupressedRecipient,

    /// <summary>
    /// Transient error, retry later
    /// </summary>
    Failed_TransientError,

    /// <summary>
    /// Failed, bounced
    /// </summary>
    Failed_Bounced,

    /// <summary>
    /// Failed, filtered spam
    /// </summary>
    Failed_FilteredSpam,

    /// <summary>
    /// Failed, quarantined
    /// </summary>
    Failed_Quarantined
}   
