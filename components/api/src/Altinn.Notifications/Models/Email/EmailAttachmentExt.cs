using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Email;

/// <summary>
/// Represents a file attachment to be included in an email notification order.
/// </summary>
public record EmailAttachmentExt
{
    /// <summary>
    /// The filename, including extension, to be shown in the recipient''s email client.
    /// </summary>
    /// <remarks>
    /// Must not contain path separators (<c>/</c>, <c>\</c>) or traversal sequences (<c>..</c>).
    /// </remarks>
    /// <example>contract.pdf</example>
    [Required]
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    /// The MIME type of the attachment.
    /// </summary>
    /// <remarks>
    /// Must be one of the MIME types accepted by Azure Communication Services.
    /// See: https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-attachment-allowed-mime-types
    /// </remarks>
    /// <example>application/pdf</example>
    [Required]
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }

    /// <summary>
    /// The SAS URL pointing to the attachment blob in Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Must be an absolute HTTPS URI with a valid <c>se</c> (signed expiry) query parameter.
    /// The expiry must be at least 15 minutes after <c>requestedSendTime</c> to cover the full
    /// processing window. The URL is stored in the order but must never be logged or included
    /// in error responses.
    /// </remarks>
    [Required]
    [JsonPropertyName("sasUrl")]
    public required string SasUrl { get; init; }
}
