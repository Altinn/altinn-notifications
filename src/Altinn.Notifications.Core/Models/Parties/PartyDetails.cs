using System.Text.Json.Serialization;

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
    /// Gets or sets the components of the person's name, if available.
    /// </summary>
    [JsonPropertyName("personName")]
    public PersonNameComponents? PersonName { get; set; }

    /// <summary>
    /// Gets or sets the social security number of the party, if applicable.
    /// </summary>
    [JsonPropertyName("ssn")]
    public string? NationalIdentityNumber { get; set; }
}
