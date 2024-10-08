﻿using System.Text.RegularExpressions;
using System.Web;

namespace Altinn.Notifications.Extensions;

/// <summary>
/// Extension class for String.
/// </summary>
public static partial class StringExtensions
{
    /// <summary>
    /// Represents a regex pattern to detect URLs in a string.
    /// </summary>
    private const string _urlPattern = @"\b((([a-zA-Z][a-zA-Z0-9+.-]*):\/\/)?(([a-zA-Z0-9_~!$&'()*+,;=.-]+(:[a-zA-Z0-9_~!$&'()*+,;=.-]+)?@)?((\d{1,3}\.){3}\d{1,3}|localhost|[a-zA-Z0-9-]+\.[a-zA-Z]{2,63}|[a-zA-Z0-9-]+\.[a-zA-Z0-9-]+\.[a-zA-Z\u00A1-\uFFFF]{2,63})(:\d+)?)(\/[\w\- .\/?%&=~!$&'()*+,;:@]*)?)\b";

    /// <summary>
    /// Checks if the passed string does not contain URLs.
    /// </summary>
    /// <param name="stringToCheck">The string to check.</param>
    /// <returns><c>true</c> if the passed string does not contain URLs; otherwise, <c>false</c>.</returns>
    public static bool DoesNotContainUrl(this string stringToCheck)
    {
        if (string.IsNullOrWhiteSpace(stringToCheck))
        {
            return true;
        }

        string decodedUrl = HttpUtility.UrlDecode(stringToCheck);

        Regex regex = new Regex(_urlPattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        return !regex.IsMatch(decodedUrl);
    }

    /// <summary>
    /// Generates a compiled regular expression based on the specified URL pattern.
    /// </summary>
    /// <returns>A <see cref="Regex"/> object that represents the compiled regular expression for detecting URLs.</returns>
    [GeneratedRegex(_urlPattern)]
    private static partial Regex UrlRegexPattern();
}
