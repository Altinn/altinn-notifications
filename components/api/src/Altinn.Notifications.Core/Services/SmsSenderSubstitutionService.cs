using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <inheritdoc cref="ISmsSenderSubstitutionService"/>
public class SmsSenderSubstitutionService : ISmsSenderSubstitutionService
{
    private readonly CompiledRule[] _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsSenderSubstitutionService"/> class.
    /// </summary>
    /// <param name="config">The sender substitution configuration.</param>
    public SmsSenderSubstitutionService(IOptions<SmsSenderSubstitutionConfig> config)
    {
        _rules = [.. config.Value.Rules
            .Where(r => !string.IsNullOrWhiteSpace(r.CountryCodePrefix) && r.NumericSenderByServiceOwner.Count > 0)
            .Select(r => new CompiledRule(r.CountryCodePrefix, r.NumericSenderByServiceOwner))];
    }

    /// <inheritdoc/>
    public bool HasRules => _rules.Length > 0;

    /// <inheritdoc/>
    public string ResolveSender(string configuredSender, string recipientPhoneNumber, string serviceOwnerShortName)
    {
        if (_rules.Length == 0 || string.IsNullOrWhiteSpace(recipientPhoneNumber) || string.IsNullOrWhiteSpace(serviceOwnerShortName))
        {
            return configuredSender;
        }

        if (!TryNormalizePhoneNumber(recipientPhoneNumber, out var normalizedPhoneNumber))
        {
            return configuredSender;
        }

        foreach (var rule in _rules)
        {
            if (!rule.Matches(normalizedPhoneNumber))
            {
                continue;
            }

            if (rule.NumericSenderByServiceOwner.TryGetValue(serviceOwnerShortName, out var numericSender) && !string.IsNullOrWhiteSpace(numericSender))
            {
                return numericSender;
            }
        }

        return configuredSender;
    }

    /// <summary>
    /// Attempts to strip a leading "+" or "00" international dialing prefix from a phone
    /// number, so that both formats resolve to the same bare country code for prefix
    /// comparison against <see cref="SmsSenderSubstitutionRule.CountryCodePrefix"/>.
    /// </summary>
    /// <param name="phoneNumber">The recipient phone number, e.g. "+4799999999" or "00479999999".</param>
    /// <param name="normalizedPhoneNumber">
    /// The phone number without its leading "+" or "00" prefix, if one was present; otherwise
    /// <see cref="string.Empty"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the phone number had a leading "+" or "00" international
    /// dialing prefix and could be normalized; otherwise <see langword="false"/>, indicating
    /// no country code substitution rule should be evaluated.
    /// </returns>
    private static bool TryNormalizePhoneNumber(string phoneNumber, out string normalizedPhoneNumber)
    {
        if (phoneNumber.StartsWith('+'))
        {
            normalizedPhoneNumber = phoneNumber[1..];
            return true;
        }

        if (phoneNumber.StartsWith("00", StringComparison.Ordinal))
        {
            normalizedPhoneNumber = phoneNumber[2..];
            return true;
        }

        normalizedPhoneNumber = string.Empty;
        return false;
    }

    /// <summary>
    /// A compiled substitution rule matching on a bare country code prefix, along with the
    /// per-service-owner numeric sender map.
    /// </summary>
    private sealed class CompiledRule
    {
        private readonly string _countryCodePrefix;

        public CompiledRule(string countryCodePrefix, Dictionary<string, string> numericSenderByServiceOwner)
        {
            _countryCodePrefix = countryCodePrefix;
            NumericSenderByServiceOwner = numericSenderByServiceOwner;
        }

        public Dictionary<string, string> NumericSenderByServiceOwner { get; }

        /// <summary>
        /// Matches an already-normalized phone number (with any leading "+" or "00" stripped)
        /// against this rule's bare country code prefix.
        /// </summary>
        public bool Matches(string normalizedPhoneNumber) =>
            normalizedPhoneNumber.StartsWith(_countryCodePrefix, StringComparison.Ordinal);
    }
}
