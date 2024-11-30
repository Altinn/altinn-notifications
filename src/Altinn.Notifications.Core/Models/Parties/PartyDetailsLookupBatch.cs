using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.Parties;

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
    public PartyDetailsLookupBatch(List<string>? organizationNumbers = null, List<string>? socialSecurityNumbers = null)
    {
        if ((organizationNumbers == null || organizationNumbers.Count == 0) && (socialSecurityNumbers == null || socialSecurityNumbers.Count == 0))
        {
            throw new ArgumentException("At least one of organizationNumbers or socialSecurityNumbers must be provided.");
        }

        PartyDetailsLookupRequestList = [];

        if (organizationNumbers != null)
        {
            PartyDetailsLookupRequestList.AddRange(organizationNumbers.Select(orgNu => new PartyDetailsLookupRequest(organizationNumber: orgNu)));
        }

        if (socialSecurityNumbers != null)
        {
            PartyDetailsLookupRequestList.AddRange(socialSecurityNumbers.Select(ssn => new PartyDetailsLookupRequest(socialSecurityNumber: ssn)));
        }
    }

    /// <summary>
    /// Gets or sets the list of lookup criteria for parties.
    /// </summary>
    [JsonPropertyName("parties")]
    public List<PartyDetailsLookupRequest>? PartyDetailsLookupRequestList { get; }
}
