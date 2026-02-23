using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.Parties;

namespace Altinn.Notifications.Integrations.Register.Models;

/// <summary>
/// Represents a request to look up party details by their identifiers.
/// </summary>
public class PartyDetailsLookupBatch
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartyDetailsLookupBatch"/> class.
    /// </summary>
    /// <param name="organizationNumbers">A list of organization numbers to look up.</param>
    /// <param name="socialSecurityNumbers">A list of social security numbers to look up.</param>
    /// <exception cref="ArgumentException">Thrown when both <paramref name="organizationNumbers"/> and <paramref name="socialSecurityNumbers"/> are null or empty.</exception>
    [JsonConstructor]
    public PartyDetailsLookupBatch(List<string>? organizationNumbers = null, List<string>? socialSecurityNumbers = null)
    {
        if ((organizationNumbers?.Count ?? 0) == 0 && (socialSecurityNumbers?.Count ?? 0) == 0)
        {
            throw new ArgumentException("You must provide either an organization number or a social security number");
        }

        OrganizationNumbers = organizationNumbers ?? [];
        SocialSecurityNumbers = socialSecurityNumbers ?? [];

        PartyDetailsLookupRequestList = [];

        if (OrganizationNumbers.Count != 0)
        {
            PartyDetailsLookupRequestList.AddRange(OrganizationNumbers.Select(orgNum => new PartyDetailsLookupRequest(organizationNumber: orgNum)));
        }

        if (SocialSecurityNumbers.Count != 0)
        {
            PartyDetailsLookupRequestList.AddRange(SocialSecurityNumbers.Select(ssn => new PartyDetailsLookupRequest(socialSecurityNumber: ssn)));
        }
    }

    /// <summary>
    /// Gets the organization numbers to look up.
    /// </summary>
    [JsonPropertyName("organizationNumbers")]
    public List<string> OrganizationNumbers { get; }

    /// <summary>
    /// Gets the social security numbers to look up.
    /// </summary>
    [JsonPropertyName("socialSecurityNumbers")]
    public List<string> SocialSecurityNumbers { get; }

    /// <summary>
    /// Gets the list of lookup criteria for parties.
    /// </summary>
    [JsonPropertyName("parties")]
    public List<PartyDetailsLookupRequest> PartyDetailsLookupRequestList { get; private set; }
}
