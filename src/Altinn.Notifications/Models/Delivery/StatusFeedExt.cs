using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Order status response object to represent a status feed entry 
/// </summary>
public record StatusFeedExt
{
    /// <summary>
    /// The sequence number of the status feed entry per creator
    /// </summary>
    [JsonPropertyName("sequenceNumber")]
    public int SequenceNumber { get; init; }

    /// <summary>
    /// The string representation of the jsonb object stored in the status feed table
    /// </summary>
    public JsonElement OrderStatus { get; init; } = default!;
}
