using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Validates <see cref="SmsSenderSubstitutionConfig"/> at startup, ensuring every configured
/// <see cref="SmsSenderSubstitutionRule.PhoneNumberPrefixPattern"/> is a syntactically valid
/// regular expression before the application starts processing notifications.
/// </summary>
public class SmsSenderSubstitutionConfigValidator : IValidateOptions<SmsSenderSubstitutionConfig>
{
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(100);

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SmsSenderSubstitutionConfig options)
    {
        if (options.Rules.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        for (var i = 0; i < options.Rules.Count; i++)
        {
            var pattern = options.Rules[i].PhoneNumberPrefixPattern;

            if (string.IsNullOrWhiteSpace(pattern))
            {
                // An empty/whitespace pattern is ignored (not applied) by SmsSenderSubstitutionService,
                // so it is not treated as a configuration error here.
                continue;
            }

            try
            {
                _ = new Regex(pattern, RegexOptions.None, _regexTimeout);
            }
            catch (ArgumentException ex)
            {
                failures.Add($"SmsSenderSubstitution.Rules[{i}].PhoneNumberPrefixPattern ('{pattern}') is not a valid regular expression: {ex.Message}");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
