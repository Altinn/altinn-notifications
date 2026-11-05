using System.Text.RegularExpressions;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// Masks credentials and personal data in LinkMobility XML payloads before they are logged.
/// </summary>
internal static partial class PayloadMasker
{
    /// <summary>
    /// Hides the password, keeps only the last two digits of each recipient
    /// and replaces message texts with their length, since they can hold one-time codes.
    /// </summary>
    public static string Mask(string? xml)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return string.Empty;
        }

        xml = PasswordRegex().Replace(xml, "<PW>***</PW>");
        xml = RecipientRegex().Replace(xml, m => $"<RCV>{DigitRegex().Replace(m.Groups[1].Value, "*")}{m.Groups[2].Value}</RCV>");
        xml = TextRegex().Replace(xml, m => $"<TEXT>[masked, {m.Groups[1].Value.Length} chars]</TEXT>");
        return xml;
    }

    /// <summary>
    /// Hides the password only.
    /// </summary>
    public static string MaskPassword(string text)
    {
        return PasswordRegex().Replace(text, "<PW>***</PW>");
    }

    /// <summary>
    /// Replaces the content of each <c>&lt;PW&gt;</c> with <c>***</c> and leaves every other byte as it is, so the rest of the body
    /// keeps its exact bytes, invalid ones included. A <c>&lt;PW&gt;</c> without an end is masked to the end of the body.
    /// </summary>
    public static byte[] MaskPassword(ReadOnlySpan<byte> body)
    {
        var masked = new MemoryStream(body.Length);
        while (body.IndexOf("<PW>"u8) is int start and >= 0)
        {
            start += "<PW>".Length;
            masked.Write(body[..start]);
            masked.Write("***"u8);

            int end = body[start..].IndexOf("</PW>"u8);
            body = end < 0 ? [] : body[(start + end)..];
        }

        masked.Write(body);
        return masked.ToArray();
    }

    /// <summary>
    /// Whether a text holds a password that is not masked, e.g. because its body was in an encoding where
    /// <see cref="MaskPassword(ReadOnlySpan{byte})"/> does not find the ASCII tags.
    /// </summary>
    public static bool HasUnmaskedPassword(string text)
    {
        return PasswordRegex().Matches(text).Any(m => m.Value != "<PW>***</PW>");
    }

    [GeneratedRegex("<PW>.*?</PW>", RegexOptions.Singleline)]
    private static partial Regex PasswordRegex();

    [GeneratedRegex(@"<RCV>(.*?)(\d{0,2})</RCV>", RegexOptions.Singleline)]
    private static partial Regex RecipientRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex DigitRegex();

    [GeneratedRegex("<TEXT>(.*?)</TEXT>", RegexOptions.Singleline)]
    private static partial Regex TextRegex();
}
