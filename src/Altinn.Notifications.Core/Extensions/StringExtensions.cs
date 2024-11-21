using System.Text.RegularExpressions;

namespace Altinn.Notifications.Core.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="string"/> class.
/// </summary>
public static partial class StringExtensions
{
    /// <summary>
    /// The regex pattern used to identify recipient name placeholders in a string.
    /// </summary>
    private static readonly Regex _recipientNamePlaceholdersKeywordsRegex = RecipientNamePlaceholdersKeywordsRegexPattern();

    /// <summary>
    /// The regex pattern used to identify recipient number placeholders in a string.
    /// </summary>
    private static readonly Regex _recipientNumberPlaceholdersKeywordsRegex = RecipientNumberPlaceholdersKeywordsRegexPattern();

    /// <summary>
    /// Checks whether the specified string contains any recipient name placeholders.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>true</c> if the string contains one or more recipient name placeholders; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// The following recipient name placeholders are supported:
    /// <list type="bullet">
    /// <item><description><c>$recipientFirstName$</c> - The first name of the recipient.</description></item>
    /// <item><description><c>$recipientMiddleName$</c> - The middle name of the recipient.</description></item>
    /// <item><description><c>$recipientLastName$</c> - The last name of the recipient.</description></item>
    /// <item><description><c>$recipientName$</c> - The full name of the recipient or organization.</description></item>
    /// </list>
    /// </remarks>
    public static bool ContainsRecipientNamePlaceholders(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return _recipientNamePlaceholdersKeywordsRegex.IsMatch(value);
    }

    /// <summary>
    /// Checks whether the specified string contains any recipient number placeholders.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns><c>true</c> if the string contains one or more recipient number placeholders; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// The following recipient number placeholders are supported:
    /// <list type="bullet">
    /// <item><description><c>recipientNumber</c> - The organization number when recipient is an organization.</description></item>
    /// </list>
    /// </remarks>
    public static bool ContainsRecipientNumberPlaceholders(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return _recipientNumberPlaceholdersKeywordsRegex.IsMatch(value);
    }

    [GeneratedRegex(@"\$recipient(FirstName|MiddleName|LastName|Name)\$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RecipientNamePlaceholdersKeywordsRegexPattern();

    [GeneratedRegex(@"\$recipient(Number)\$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RecipientNumberPlaceholdersKeywordsRegexPattern();
}
