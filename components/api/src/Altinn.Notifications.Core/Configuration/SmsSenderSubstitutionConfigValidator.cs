using Altinn.Notifications.Core.Helpers;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Validates <see cref="SmsSenderSubstitutionConfig"/> at startup, ensuring every configured
/// <see cref="SmsSenderSubstitutionRule.CountryCodePrefix"/> consists of 1-3 digits only, with
/// no leading "+"/"00" or regex metacharacters, and that every configured numeric sender number
/// is a valid mobile number, before the application starts processing notifications.
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
            var rule = options.Rules[i];
            var prefix = rule.CountryCodePrefix;

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

            foreach (var (serviceOwner, numericSender) in rule.NumericSenderByServiceOwner)
            {
                if (string.IsNullOrWhiteSpace(numericSender))
                {
                    continue;
                }

                if (!MobileNumberHelper.IsValidMobileNumber(numericSender))
                {
                    failures.Add($"SmsSenderSubstitution.Rules[{i}].NumericSenderByServiceOwner['{serviceOwner}'] ('{numericSender}') is not a valid mobile number.");
                }
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
