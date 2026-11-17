using System.Text.RegularExpressions;

namespace Altinn.Notifications.Tools.Shared;

/// <summary>
/// Makes a value from a request safe to put in a log entry. Control characters and line and paragraph separators are written
/// as escapes, so the value cannot start a new line, and with it a fake log entry, in a text log.
/// Linked into each tool that logs request data, since the tools have no shared library.
/// </summary>
/// <remarks>
/// CodeQL's log forging query (<c>cs/log-forging</c>) recognises <see cref="Regex.Replace(string, MatchEvaluator)"/> as a sanitizer,
/// so values passed through <see cref="Clean"/> are not flagged.
/// </remarks>
internal static partial class LogSanitizer
{
    /// <summary>
    /// Returns <paramref name="value"/> with control characters and line and paragraph separators escaped, e.g. a line feed as <c>\n</c>.
    /// </summary>
    public static string Clean(string? value) => value is null ? string.Empty : UnsafeCharacters().Replace(value, Escape);

    private static string Escape(Match match) => match.Value[0] switch
    {
        '\r' => @"\r",
        '\n' => @"\n",
        '\t' => @"\t",
        char c => $@"\u{(int)c:x4}",
    };

    [GeneratedRegex(@"[\p{Cc}\p{Zl}\p{Zp}]")]
    private static partial Regex UnsafeCharacters();
}
