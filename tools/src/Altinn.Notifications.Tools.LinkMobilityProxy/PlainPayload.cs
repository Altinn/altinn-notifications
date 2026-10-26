using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// Describes a body for <see cref="Configuration.PayloadLogMode.Plain"/> logging: the text with only the password masked,
/// the text with invisible characters escaped, and the hex of its bytes.
/// </summary>
internal static class PlainPayload
{
    /// <summary>
    /// The body with only the password masked, at byte level. Null when the password could not be found at byte level, e.g. in
    /// a UTF-16 body, so the bytes are not shown at all.
    /// </summary>
    public static byte[]? MaskedBytes(byte[] body, string? contentType)
    {
        var masked = PayloadMasker.MaskPassword(body);
        return PayloadMasker.HasUnmaskedPassword(Encoding(contentType).GetString(masked)) ? null : masked;
    }

    /// <summary>
    /// A block with the content type, the length, the text decoded by the content type's charset, the escaped text and the hex,
    /// all with the password masked.
    /// </summary>
    public static string Describe(string label, byte[] body, string? contentType)
    {
        var encoding = Encoding(contentType);
        var masked = MaskedBytes(body, contentType);
        string text = masked is null ? PayloadMasker.MaskPassword(encoding.GetString(body)) : encoding.GetString(masked);

        return new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{label}: {contentType ?? "no content type"}, {body.Length} bytes, decoded as {encoding.WebName}\n")
            .Append(text).Append('\n')
            .Append("Escaped: ").Append(Escape(text)).Append('\n')
            .Append("Hex: ").Append(masked is null ? "[left out: the password could not be masked in the bytes]" : Hex(masked))
            .ToString();
    }

    /// <summary>
    /// Shows characters that cannot be seen or told apart when rendered: <c>\r</c>, <c>\n</c> and <c>\t</c>, other control characters
    /// as <c>\u0007</c>, and format characters, non-breaking and other special spaces, combining marks and the replacement character
    /// that invalid bytes decode to as <c>[U+00A0]</c>. A backslash becomes <c>\\</c>, so the result is never ambiguous.
    /// </summary>
    public static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '\\': escaped.Append(@"\\"); break;
                case '\r': escaped.Append(@"\r"); break;
                case '\n': escaped.Append(@"\n"); break;
                case '\t': escaped.Append(@"\t"); break;
                default:
                    if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        // A character outside the BMP, e.g. an emoji, is visible as it is.
                        escaped.Append(c).Append(text[++i]);
                    }
                    else if (char.IsControl(c))
                    {
                        escaped.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}");
                    }
                    else if (IsInvisible(c))
                    {
                        escaped.Append(CultureInfo.InvariantCulture, $"[U+{(int)c:X4}]");
                    }
                    else
                    {
                        escaped.Append(c);
                    }

                    break;
            }
        }

        return escaped.ToString();
    }

    /// <summary>
    /// The bytes as space separated hex pairs, e.g. <c>3C 50 57 3E</c>.
    /// </summary>
    public static string Hex(byte[] bytes)
    {
        return string.Join(' ', bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// The content type's charset, or UTF-8 when it has none or one .NET does not know.
    /// </summary>
    public static Encoding Encoding(string? contentType)
    {
        if (MediaTypeHeaderValue.TryParse(contentType, out var mediaType) && !string.IsNullOrWhiteSpace(mediaType.CharSet))
        {
            try
            {
                return System.Text.Encoding.GetEncoding(mediaType.CharSet.Trim('"'));
            }
            catch (ArgumentException)
            {
            }
        }

        return System.Text.Encoding.UTF8;
    }

    private static bool IsInvisible(char c)
    {
        return c == '\uFFFD' || char.GetUnicodeCategory(c) switch
        {
            UnicodeCategory.SpaceSeparator => c != ' ',
            UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator => true,
            UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark => true,
            UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned => true,
            _ => false,
        };
    }
}
