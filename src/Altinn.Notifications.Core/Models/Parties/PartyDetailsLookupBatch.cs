using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models.Parties;

/// <summary>
/// Represents a request to look up party details by their identifiers.
/// </summary>
public class PartyDetailsLookupBatch
{
    /// <summary>
    /// Gets or sets the list of lookup criteria for parties.
    /// </summary>
    [JsonPropertyName("parties")]
    public List<PartyDetailsLookupRequest>? PartyDetailsLookupRequestList { get; set; }
}
