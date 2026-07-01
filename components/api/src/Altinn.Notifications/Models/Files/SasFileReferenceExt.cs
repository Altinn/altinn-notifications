using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Files;

/// <summary>
/// Identifies a file in Azure Blob Storage by its SAS URL, name, and MIME type.
/// </summary>
/// <remarks>
/// This record is a reference only — it does not carry file content. The caller uploads
/// the file to Azure Blob Storage, generates a SAS URL, and includes this object in the
/// order request. The notification service uses the SAS URL to download the file at send time.
/// </remarks>
public record SasFileReferenceExt
{
    /// <summary>
    /// The filename, including extension (e.g. <c>contract.pdf</c>).
    /// </summary>
    /// <remarks>
    /// Must not contain path separators (<c>/</c>, <c>\</c>) or traversal sequences (<c>..</c>).
    /// </remarks>
    /// <example>contract.pdf</example>
    [Required]
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    /// The MIME type of the file (e.g. <c>application/pdf</c>).
    /// </summary>
    /// <remarks>
    /// Must be one of the MIME types accepted by Azure Communication Services.
    /// </remarks>
    /// <example>application/pdf</example>
    [Required]
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }

    /// <summary>
    /// The SAS URL granting time-limited read access to the file in Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Must be an absolute HTTPS URI containing the required SAS parameters (<c>se</c>, <c>sig</c>,
    /// <c>sp</c>, <c>sr</c>) with at least read permission. The expiry (<c>se</c>) must be at least
    /// 15 minutes after <c>requestedSendTime</c>. Must never be logged or included in error responses.
    /// </remarks>
    [Required]
    [JsonPropertyName("sasUrl")]
    public required Uri SasUrl { get; init; }
}
