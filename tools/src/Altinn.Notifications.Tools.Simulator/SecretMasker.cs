using System.Text.RegularExpressions;

namespace Altinn.Notifications.Tools.Simulator;

/// <summary>
/// Hides credentials before a request is shown in the GUI. The simulator itself keeps and forwards nothing but the answer.
/// </summary>
internal static partial class SecretMasker
{
    private static readonly HashSet<string> _secretHeaders = new(StringComparer.OrdinalIgnoreCase) { "Authorization", "Proxy-Authorization", "Cookie" };

    /// <summary>
    /// Keeps the scheme of credential headers, e.g. <c>Bearer ***</c> or <c>HMAC-SHA256 ***</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> MaskHeaders(IReadOnlyDictionary<string, string> headers) =>
        headers.ToDictionary(h => h.Key, h => _secretHeaders.Contains(h.Key) ? MaskCredential(h.Value) : h.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hides the LinkMobility password, <c>&lt;PW&gt;…&lt;/PW&gt;</c>.
    /// </summary>
    public static string MaskBody(string body) => PasswordRegex().Replace(body, "<PW>***</PW>");

    private static string MaskCredential(string value)
    {
        int space = value.IndexOf(' ');
        return space > 0 ? value[..space] + " ***" : "***";
    }

    [GeneratedRegex("<PW>.*?</PW>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex PasswordRegex();
}
