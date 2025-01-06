using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.Parties;

/// <summary>
/// Represents a lookup criterion for a single party.
/// </summary>
public record PartyDetailsLookupRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartyDetailsLookupRequest"/> class.
    /// Ensures that only one of <see cref="OrganizationNumber"/> or <see cref="SocialSecurityNumber"/> is set.
    /// </summary>
    /// <param name="organizationNumber">The organization number of the party.</param>
    /// <param name="socialSecurityNumber">The social security number of the party.</param>
    /// <exception cref="ArgumentException">Thrown when both <paramref name="organizationNumber"/> and <paramref name="socialSecurityNumber"/> are set.</exception>
    public PartyDetailsLookupRequest(string? organizationNumber = null, string? socialSecurityNumber = null)
    {
        if (string.IsNullOrWhiteSpace(organizationNumber) && string.IsNullOrWhiteSpace(socialSecurityNumber))
        {
            throw new ArgumentException("You must provide either an organization number or a social security number.");
        }

        if (!string.IsNullOrWhiteSpace(organizationNumber) && !string.IsNullOrWhiteSpace(socialSecurityNumber))
        {
            throw new ArgumentException("You can provide either an organization number or a social security number, but not both.");
        }

        OrganizationNumber = organizationNumber;
        SocialSecurityNumber = socialSecurityNumber;
    }

    /// <summary>
    /// Gets the organization number of the party.
    /// </summary>
    /// <value>The organization number of the party.</value>
    [JsonPropertyName("orgNo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrganizationNumber { get; }

    /// <summary>
    /// Gets the social security number of the party.
    /// </summary>
    /// <value>The social security number of the party.</value>
    [JsonPropertyName("ssn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SocialSecurityNumber { get; }
}
