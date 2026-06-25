namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a file attachment to be included in an email notification.
/// </summary>
/// <remarks>
/// Attachments are referenced by SAS URL. The email service downloads and base64-encodes
/// the file at send time. The SAS URL must remain valid throughout the full processing window.
/// </remarks>
public record EmailAttachment
{
    /// <summary>
    /// The filename, including extension, to be shown in the recipient''s email client.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// The MIME type of the attachment (e.g. "application/pdf").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// The SAS URL pointing to the attachment blob in Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Must never be logged or included in error responses.
    /// </remarks>
    public required string SasUrl { get; init; }
}
