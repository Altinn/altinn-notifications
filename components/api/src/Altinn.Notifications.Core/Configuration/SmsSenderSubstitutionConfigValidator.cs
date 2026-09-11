using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Validates <see cref="SmsSenderSubstitutionConfig"/> at startup, ensuring every configured
/// <see cref="SmsSenderSubstitutionRule.CountryCodePrefix"/> consists of 1-3 digits only, with
/// no leading "+"/"00" or regex metacharacters, before the application starts processing
/// notifications.
/// </summary>
public class SmsSenderSubstitutionConfigValidator : IValidateOptions<SmsSenderSubstitutionConfig>
{
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
            var prefix = options.Rules[i].CountryCodePrefix;

            if (string.IsNullOrWhiteSpace(prefix))
            {
                // An empty/whitespace prefix is ignored (not applied) by SmsSenderSubstitutionService,
                // so it is not treated as a configuration error here.
                continue;
            }

            if (prefix.Length is < 1 or > 3 || !prefix.All(char.IsAsciiDigit))
            {
                failures.Add($"SmsSenderSubstitution.Rules[{i}].CountryCodePrefix ('{prefix}') must consist of 1-3 digits only, without a leading '+' or '00'.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
