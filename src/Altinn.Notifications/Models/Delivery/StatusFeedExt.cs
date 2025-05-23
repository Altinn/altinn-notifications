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
    public required int SequenceNumber { get; init; }

    /// <summary>
    /// A JsonElement that represents of the content of the order status object
    /// </summary>
    [JsonPropertyName("orderStatus")]
    public required JsonElement OrderStatus { get; init; }
}
