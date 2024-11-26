using System.Text.RegularExpressions;

namespace Altinn.Notifications.Core.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="string"/> class.
/// </summary>
public static partial class StringExtensions
{
    private static readonly Regex _recipientNamePlaceholderRegex = RecipientNamePlaceholderKeywordRegex();
    private static readonly Regex _recipientNumberPlaceholderRegex = RecipientNumberPlaceholderKeywordRegex();

    /// <summary>
    /// Checks whether the specified string contains the placeholder keyword $recipientName$.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>true</c> if the string contains the placeholder keyword $recipientName$; otherwise, <c>false</c>.</returns>
    public static bool ContainsRecipientNamePlaceholder(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return _recipientNamePlaceholderRegex.IsMatch(value);
    }

    /// <summary>
    /// Checks whether the specified string contains the placeholder keyword $recipientNumber$.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>true</c> if the string contains the placeholder keyword $recipientNumber$; otherwise, <c>false</c>.</returns>
    public static bool ContainsRecipientNumberPlaceholder(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return _recipientNumberPlaceholderRegex.IsMatch(value);
    }

    /// <summary>
    /// The regex pattern used to identify $recipientName$ in a string.
    /// </summary>
    [GeneratedRegex(@"\$recipientName\$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RecipientNamePlaceholderKeywordRegex();

    /// <summary>
    /// The regex pattern used to identify $recipientNumber$ in a string.
    /// </summary>
    [GeneratedRegex(@"\$recipientNumber\$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RecipientNumberPlaceholderKeywordRegex();
}
