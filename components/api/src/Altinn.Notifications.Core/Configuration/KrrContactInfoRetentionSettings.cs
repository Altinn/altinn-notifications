namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Settings controlling whether contact information from the Contact and Reservation Register (KRR)
/// that has not been updated or confirmed within a retention window may be used for notifications.
/// </summary>
/// <remarks>
/// Reverts to the legacy Altinn 2 behaviour where outdated KRR contact information is not used,
/// for all service owners except those explicitly exempted in <see cref="ExemptServiceOwners"/>
/// (i.e. service owners with a deliberate, compensating process around outdated contact information).
/// </remarks>
public class KrrContactInfoRetentionSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether outdated KRR contact information should be filtered out.
    /// Defaults to <c>true</c> (restrictive by default).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum age, in months, that KRR contact information may have since it was last
    /// updated or confirmed before it is considered outdated and treated as if it is not present. Defaults to 18.
    /// </summary>
    public int MaxAgeMonths { get; set; } = 18;

    /// <summary>
    /// Gets or sets the list of service owner short names (e.g. "skd") that are exempt from the retention check
    /// and therefore retain the behaviour of using contact information regardless of its age.
    /// </summary>
    public List<string> ExemptServiceOwners { get; set; } = [];
}
