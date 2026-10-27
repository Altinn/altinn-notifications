namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// A class respresenting a user contact point lookup object
/// </summary>
public class UserContactPointLookup
{
    /// <summary>
    /// A list of national identity numbers to look up contact points or contact point availability for
    /// </summary>
    public List<string> NationalIdentityNumbers { get; set; } = [];

    /// <summary>
    /// Instructs the profile service to return contact information even if it is considered stale,
    /// which means it may not have been updated recently. (Currently 18 months since last update or verification.)
    /// </summary>
    public bool UseStaleContactInfo { get; set; } = false;
}
