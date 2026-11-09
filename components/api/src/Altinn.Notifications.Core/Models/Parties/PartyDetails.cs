using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Parties;

/// <summary>
/// Represents the details for a specific party.
/// </summary>
public class PartyDetails
{
    /// <summary>
    /// Gets or sets the name of the party.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the organization number of the party, if applicable.
    /// </summary>
    [JsonPropertyName("orgNo")]
    public string? OrganizationNumber { get; set; }

    /// <summary>
    /// Gets or sets the social security number of the party, if applicable.
    /// </summary>
    [JsonPropertyName("ssn")]
    public string? NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets the registry the organization originates from.
    /// Only populated when explicitly requested from Register.
    /// </summary>
    [JsonPropertyName("source")]
    public OrganizationRegistrySource? RegistrySource { get; set; }
}
