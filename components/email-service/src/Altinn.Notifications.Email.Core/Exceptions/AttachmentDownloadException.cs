namespace Altinn.Notifications.Email.Core.Exceptions;

/// <summary>
/// Thrown when a transient network or HTTP error occurs while downloading an email attachment.
/// This is a retriable failure — the message should be retried according to the configured policy.
/// </summary>
public class AttachmentDownloadException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="AttachmentDownloadException"/> for a transient HTTP error.
    /// </summary>
    /// <param name="filename">The filename of the attachment that failed to download.</param>
    /// <param name="httpStatusCode">The HTTP status code returned by the blob storage endpoint.</param>
    /// <param name="inner">The underlying HTTP exception.</param>
    public AttachmentDownloadException(string filename, int httpStatusCode, Exception inner)
        : base($"Transient HTTP {httpStatusCode} error downloading attachment '{filename}'.", inner)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AttachmentDownloadException"/> for a network or unknown error.
    /// </summary>
    /// <param name="filename">The filename of the attachment that failed to download.</param>
    /// <param name="inner">The underlying network exception.</param>
    public AttachmentDownloadException(string filename, Exception inner)
        : base($"Network error downloading attachment '{filename}'.", inner)
    {
    }
}
