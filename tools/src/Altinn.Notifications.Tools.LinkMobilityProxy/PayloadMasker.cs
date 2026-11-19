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

    [GeneratedRegex("<PW>.*?</PW>", RegexOptions.Singleline)]
    private static partial Regex PasswordRegex();

    [GeneratedRegex(@"<RCV>(.*?)(\d{0,2})</RCV>", RegexOptions.Singleline)]
    private static partial Regex RecipientRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex DigitRegex();

    [GeneratedRegex("<TEXT>(.*?)</TEXT>", RegexOptions.Singleline)]
    private static partial Regex TextRegex();
}
