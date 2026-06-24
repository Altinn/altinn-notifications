using System.Globalization;
using System.Web;

using Altinn.Notifications.Models.Email;

using FluentValidation;

namespace Altinn.Notifications.Validators.Rules;

/// <summary>
/// Provides FluentValidation extension methods and helpers for validating <see cref="EmailAttachmentExt"/> properties.
/// </summary>
internal static class EmailAttachmentRules
{
    /// <summary>
    /// MIME types accepted by Azure Communication Services for email attachments.
    /// </summary>
    /// <remarks>
    /// Source: https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-attachment-allowed-mime-types
    /// </remarks>
    private static readonly IReadOnlySet<string> _allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
    /// Adds validation rules for an attachment filename: must be non-empty, must not contain
    /// path separators (<c>/</c>, <c>\</c>) or traversal sequences (<c>..</c>), and must include
    /// a file extension.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder to which the validation rules will be added.</param>
    /// <returns>The rule builder options with the added validation rules.</returns>
    internal static IRuleBuilderOptions<T, string> ValidateAttachmentFilename<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder.ChildRules(rules =>
        {
            rules.RuleFor(f => f)
                .NotEmpty()
                .WithMessage("Attachment filename must not be empty.");

            rules.RuleFor(f => f)
                .Must(IsValidFilename)
                .When(f => !string.IsNullOrEmpty(f))
                .WithMessage("Attachment filename must not contain path separators or traversal sequences, and must include a file extension.");
        });
    }

    /// <summary>
    /// Adds validation rules for an attachment MIME type: must be non-empty and must be one of
    /// the MIME types accepted by Azure Communication Services.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder to which the validation rules will be added.</param>
    /// <returns>The rule builder options with the added validation rules.</returns>
    internal static IRuleBuilderOptions<T, string> ValidateAttachmentMimeType<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder.ChildRules(rules =>
        {
            rules.RuleFor(m => m)
                .NotEmpty()
                .WithMessage("Attachment mimeType must not be empty.");

            rules.RuleFor(m => m)
                .Must(_allowedMimeTypes.Contains)
                .When(m => !string.IsNullOrEmpty(m))
                .WithMessage("Attachment mimeType is not supported. Refer to ACS documentation for the list of accepted MIME types.");
        });
    }

    /// <summary>
    /// Adds validation rules for an attachment SAS URL: must be non-empty, must be an absolute
    /// HTTPS URI, and must contain a parseable <c>se</c> (signed expiry) query parameter.
    /// </summary>
    /// <typeparam name="T">The type of the object being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder to which the validation rules will be added.</param>
    /// <returns>The rule builder options with the added validation rules.</returns>
    internal static IRuleBuilderOptions<T, string> ValidateAttachmentSasUrl<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder.ChildRules(rules =>
        {
            rules.RuleFor(url => url)
                .NotEmpty()
                .WithMessage("Attachment sasUrl must not be empty.");

            rules.RuleFor(url => url)
                .Must(IsAbsoluteHttpsUri)
                .When(url => !string.IsNullOrEmpty(url))
                .WithMessage("Attachment sasUrl must be an absolute HTTPS URI.");

            rules.RuleFor(url => url)
                .Must(url => ParseSasExpiry(url) != null)
                .When(url => !string.IsNullOrEmpty(url) && IsAbsoluteHttpsUri(url))
                .WithMessage("Attachment sasUrl must contain a valid 'se' (signed expiry) query parameter.");
        });
    }

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

        if (string.IsNullOrEmpty(seValue))
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
    /// <param name="filename">The filename to validate.</param>
    private static bool IsValidFilename(string filename) =>
        !filename.Contains('/')
        && !filename.Contains('\\')
        && !filename.Contains("..")
        && Path.GetExtension(filename).Length > 1;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="url"/> is an absolute URI using the
    /// HTTPS scheme.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    private static bool IsAbsoluteHttpsUri(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
}
