namespace Altinn.Notifications.Core.Models.Files;

/// <summary>
/// Identifies a file in Azure Blob Storage by its SAS URL, name, and MIME type.
/// </summary>
/// <remarks>
/// This record is a reference only — it does not carry file content.
/// The SAS URL must remain valid throughout the full processing window so the
/// notification service can download the file when needed.
/// </remarks>
public record SasFileReference
{
    /// <summary>
    /// The filename, including extension (e.g. <c>contract.pdf</c>).
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// The MIME type of the file (e.g. <c>application/pdf</c>).
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// The SAS URL granting time-limited read access to the file in Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Must never be logged or included in error responses.
    /// </remarks>
    public required string SasUrl { get; init; }

    /// <summary>
    /// Returns a safe string representation that excludes <see cref="SasUrl"/> to prevent
    /// accidental exposure of live SAS tokens in logs, exception messages, or debugger output.
    /// </summary>
    public override string ToString() =>
        $"SasFileReference {{ Filename = {Filename}, MimeType = {MimeType}, SasUrl = [redacted] }}";
}
