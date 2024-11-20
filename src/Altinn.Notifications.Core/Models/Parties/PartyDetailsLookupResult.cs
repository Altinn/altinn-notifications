using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.Parties;

/// <summary>
/// Represents the response for a party details lookup operation.
/// </summary>
public class PartyDetailsLookupResult
{
    /// <summary>
    /// Gets or sets the list of party details.
    /// </summary>
    [JsonPropertyName("partyNames")]
    public List<PartyDetails>? PartyDetailsList { get; set; }
}
