namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration for substituting alphanumeric SMS sender IDs with numeric sender numbers,
/// required in countries where local legislation makes alphanumeric SenderID unfeasible.
/// </summary>
public class SmsSenderSubstitutionConfig
{
    /// <summary>
    /// Substitution rules evaluated in order. The first rule whose
    /// <see cref="SmsSenderSubstitutionRule.CountryCodePrefix"/> matches the
    /// recipient's phone number, and that has an entry for the relevant service owner,
    /// is applied.
    /// </summary>
    public List<SmsSenderSubstitutionRule> Rules { get; set; } = [];
}

/// <summary>
/// A single sender substitution rule: a country calling code mapped to numeric
/// sender numbers per service owner (creator short name).
/// </summary>
public class SmsSenderSubstitutionRule
{
    /// <summary>
    /// The bare country calling code, consisting of 1-3 digits only (e.g. "47" for Norway).
    /// Must NOT include a leading "+", "00", or any regex metacharacters. Matching is done
    /// against the recipient's phone number after normalizing away either a leading "+" or
    /// "00" international dialing prefix, so a rule configured with "47" matches both
    /// "+4799999999" and "00479999999".
    /// </summary>
    public string CountryCodePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Maps a service owner's short name (e.g. "digdir") to the numeric sender number to
    /// use instead of the alphanumeric default for that owner.
    /// </summary>
    public Dictionary<string, string> NumericSenderByServiceOwner { get; set; } = [];
}
