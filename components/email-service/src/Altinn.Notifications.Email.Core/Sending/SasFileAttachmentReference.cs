namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Represents attachment metadata and SAS URL required by the email core domain
/// to download and attach a blob when sending composed email.
/// </summary>
public sealed record SasFileAttachmentReference
{
    /// <summary>
    /// Gets or sets the attachment file name including extension.
    /// </summary>
    public string Filename { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the attachment.
    /// </summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the SAS URL granting temporary read access to the blob.
    /// </summary>
    public string SasUrl { get; init; } = string.Empty;
}
