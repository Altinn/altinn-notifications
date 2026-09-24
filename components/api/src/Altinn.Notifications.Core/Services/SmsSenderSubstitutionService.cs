using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <inheritdoc cref="ISmsSenderSubstitutionService"/>
/// <summary>
/// Initializes a new instance of the <see cref="SmsSenderSubstitutionService"/> class.
/// </summary>
/// <param name="config">The sender substitution configuration.</param>
public class SmsSenderSubstitutionService(IOptions<SmsSenderSubstitutionConfig> config) : ISmsSenderSubstitutionService
{
    private readonly CompiledRule[] _rules = [.. config.Value.Rules
            .Where(r => !string.IsNullOrWhiteSpace(r.CountryCodePrefix) && r.NumericSenderByServiceOwner.Count > 0)
            .Select(r => new CompiledRule(r.CountryCodePrefix, FilterToValidNumericSenders(r.NumericSenderByServiceOwner)))
            .Where(r => r.NumericSenderByServiceOwner.Count > 0)];

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
    /// Filters a service-owner-to-numeric-sender map down to entries whose numeric sender is a
    /// valid mobile number, using the same validation applied to recipient phone numbers
    /// elsewhere in the application. Invalid entries are dropped so that, if encountered, the
    /// original <c>configuredSender</c> is used instead of an invalid numeric sender.
    /// </summary>
    /// <param name="numericSenderByServiceOwner">The configured service-owner-to-numeric-sender map.</param>
    /// <returns>A new map containing only entries with a valid numeric sender.</returns>
    private static Dictionary<string, string> FilterToValidNumericSenders(Dictionary<string, string> numericSenderByServiceOwner)
    {
        return numericSenderByServiceOwner
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value) && MobileNumberHelper.IsValidMobileNumber(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
