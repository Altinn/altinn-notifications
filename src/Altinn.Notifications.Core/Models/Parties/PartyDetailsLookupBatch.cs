﻿using System.Text.Json.Serialization;

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
    /// <exception cref="ArgumentException">Thrown when both <paramref name="organizationNumbers"/> and <paramref name="socialSecurityNumbers"/> are provided simultaneously or when both are null or empty.</exception>
    [JsonConstructor]
    public PartyDetailsLookupBatch(List<string>? organizationNumbers = null, List<string>? socialSecurityNumbers = null)
    {
        if ((organizationNumbers == null || organizationNumbers.Count == 0) && (socialSecurityNumbers == null || socialSecurityNumbers.Count == 0))
        {
            throw new ArgumentException("At least one of organizationNumbers or socialSecurityNumbers must be provided.");
        }

        if (organizationNumbers != null && organizationNumbers.Count > 0 && socialSecurityNumbers != null && socialSecurityNumbers.Count > 0)
        {
            throw new ArgumentException("Both organizationNumbers and socialSecurityNumbers cannot be provided simultaneously. Please provide only one.");
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
