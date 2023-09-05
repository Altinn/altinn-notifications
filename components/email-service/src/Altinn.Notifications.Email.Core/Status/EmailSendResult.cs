namespace Altinn.Notifications.Email.Core.Status;

/// <summary>
/// Enum describing email send result types
/// </summary>
public enum EmailSendResult
{
    /// <summary>
    /// Failed, to be specified
    /// </summary>
    Failed,

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
    Delivered
}
