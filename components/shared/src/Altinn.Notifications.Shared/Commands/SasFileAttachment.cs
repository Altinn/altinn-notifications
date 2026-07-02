using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Describes a single file attachment carried inside a <see cref="SendComposedEmailCommand"/>.
/// </summary>
public sealed record SasFileAttachment
{
    /// <summary>The filename including extension (e.g. <c>contract.pdf</c>).</summary>
    [JsonPropertyName("filename")]
    public string Filename { get; init; } = string.Empty;

    /// <summary>The MIME type of the file (e.g. <c>application/pdf</c>).</summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = string.Empty;

    /// <summary>
    /// The SAS URL granting time-limited read access to the file in Azure Blob Storage.
    /// </summary>
    [JsonPropertyName("sasUrl")]
    public string SasUrl { get; init; } = string.Empty;
}
