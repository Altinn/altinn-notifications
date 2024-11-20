using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.Parties;

/// <summary>
/// Represents a lookup criterion for a single party.
/// </summary>
public record PartyDetailsLookupRequest
{
    /// <summary>
    /// Gets or sets the organization number of the party.
    /// </summary>
    /// <value>The organization number of the party.</value>
    [JsonPropertyName("orgNo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrganizationNumber { get; init; }

    /// <summary>
    /// Gets or sets the social security number of the party.
    /// </summary>
    /// <value>The social security number of the party.</value>
    [JsonPropertyName("ssn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SocialSecurityNumber { get; init; }
}
