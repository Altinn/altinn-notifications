namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration for substituting alphanumeric SMS sender IDs with numeric sender numbers,
/// required in countries where local legislation makes alphanumeric SenderID unfeasible.
/// </summary>
public class SmsSenderSubstitutionConfig
{
    /// <summary>
    /// Substitution rules evaluated in order. The first rule whose
    /// <see cref="SmsSenderSubstitutionRule.PhoneNumberPrefixPattern"/> matches the
    /// recipient's phone number, and that has an entry for the relevant service owner,
    /// is applied.
    /// </summary>
    public List<SmsSenderSubstitutionRule> Rules { get; set; } = [];
}

/// <summary>
/// A single sender substitution rule: a phone number prefix pattern mapped to numeric
/// sender numbers per service owner (creator short name).
/// </summary>
public class SmsSenderSubstitutionRule
{
    /// <summary>
    /// A regular expression matched against the recipient's phone number.
    /// </summary>
    /// <remarks>
    /// If the pattern is a simple literal prefix (no regex metacharacters other than an
    /// optional leading "^"), it can be matched using a fast ordinal string comparison
    /// instead of a full regex evaluation.
    /// </remarks>
    public string PhoneNumberPrefixPattern { get; set; } = string.Empty;

    /// <summary>
    /// Maps a service owner's short name (e.g. "digdir") to the numeric sender number to
    /// use instead of the alphanumeric default for that owner.
    /// </summary>
    public Dictionary<string, string> NumericSenderByServiceOwner { get; set; } = [];
}
