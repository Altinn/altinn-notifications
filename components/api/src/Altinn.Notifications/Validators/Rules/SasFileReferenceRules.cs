using System.Globalization;
using System.Web;

using Altinn.Notifications.Models.Files;

namespace Altinn.Notifications.Validators.Rules;

/// <summary>
/// Provides helpers for validating <see cref="SasFileReferenceExt"/> properties.
/// </summary>
internal static class SasFileReferenceRules
{
    /// <summary>
    /// MIME types accepted by Azure Communication Services for email attachments.
    /// </summary>
    /// <remarks>
    /// Source: https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-attachment-allowed-mime-types
    /// </remarks>
    private static readonly HashSet<string> _allowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/3gpp",
        "video/3gpp2",
        "application/x-7z-compressed",
        "audio/aac",
        "video/x-msvideo",
        "image/bmp",
        "text/csv",
        "application/msword",
        "application/vnd.ms-word.document.macroEnabled.12",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-fontobject",
        "application/epub+zip",
        "image/gif",
        "application/gzip",
        "image/vnd.microsoft.icon",
        "text/calendar",
        "image/jpeg",
        "application/json",
        "audio/midi",
        "audio/mpeg",
        "video/mp4",
        "video/mpeg",
        "audio/ogg",
        "video/ogg",
        "application/ogg",
        "application/onenote",
        "audio/opus",
        "font/otf",
        "application/pdf",
        "image/png",
        "application/vnd.ms-powerpoint.slideshow.macroEnabled.12",
        "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
        "application/vnd.ms-powerpoint",
        "application/vnd.ms-powerpoint.presentation.macroEnabled.12",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.ms-publisher",
        "application/x-rar-compressed",
        "application/vnd.ms-outlook",
        "application/rtf",
        "image/svg+xml",
        "application/x-tar",
        "image/tiff",
        "font/ttf",
        "text/plain",
        "application/vnd.visio",
        "audio/wav",
        "audio/webm",
        "video/webm",
        "image/webp",
        "audio/x-ms-wma",
        "video/x-ms-wmv",
        "font/woff",
        "font/woff2",
        "application/vnd.ms-excel",
        "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
        "application/vnd.ms-excel.sheet.macroEnabled.12",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/xml",
        "text/xml",
        "application/zip"
    };

    /// <summary>
    /// Returns <see langword="true"/> if the <c>sp</c> (signed permissions) parameter of <paramref name="url"/>
    /// contains the read (<c>r</c>) permission.
    /// </summary>
    internal static bool HasReadPermission(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var sp = HttpUtility.ParseQueryString(uri.Query)["sp"];
        return sp != null && sp.Contains('r', StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="url"/> is an absolute URI using the HTTPS scheme
    /// with a host in the Azure Blob Storage domain (<c>*.blob.core.windows.net</c>).
    /// </summary>
    internal static bool IsAbsoluteHttpsUri(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> if the host of <paramref name="url"/> is within
    /// the Azure Blob Storage domain (<c>*.blob.core.windows.net</c>).
    /// </summary>
    internal static bool IsAzureBlobStorageHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses the <c>se</c> (signed expiry) query parameter from a SAS URL.
    /// </summary>
    /// <param name="url">The SAS URL to parse.</param>
    /// <returns>
    /// The parsed <see cref="DateTime"/> expiry value, or <see langword="null"/> if the URL is
    /// not a valid absolute URI, the <c>se</c> parameter is absent, or the value cannot be parsed.
    /// </returns>
    internal static DateTime? ParseSasExpiry(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var seValue = HttpUtility.ParseQueryString(uri.Query)["se"];

        if (string.IsNullOrWhiteSpace(seValue))
        {
            return null;
        }

        return DateTime.TryParse(seValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry)
            ? expiry
            : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="filename"/> is safe to use as an attachment
    /// filename: no forward or backward slashes, no <c>..</c> traversal sequences, and includes
    /// a file extension of at least one character after the dot.
    /// </summary>
    internal static bool IsValidFilename(string filename) =>
        !filename.Contains('/')
        && !filename.Contains('\\')
        && Path.GetExtension(filename).Length > 1;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="url"/> contains all required
    /// blob SAS query parameters and targets a blob resource (<c>sr=b</c>).
    /// </summary>
    internal static bool HasRequiredSasParameters(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var query = HttpUtility.ParseQueryString(uri.Query);

        return !string.IsNullOrWhiteSpace(query["se"])
            && !string.IsNullOrWhiteSpace(query["sp"])
            && string.Equals(query["sr"], "b", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(query["sig"]);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="mimeType"/> is accepted by Azure Communication Services.
    /// </summary>
    internal static bool IsAllowedMimeType(string mimeType) => _allowedMimeTypes.Contains(mimeType);
}
