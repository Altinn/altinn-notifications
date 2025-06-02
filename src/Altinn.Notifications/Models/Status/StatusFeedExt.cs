using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Status;

/// <summary>
/// Order status response object to represent a status feed entry 
/// </summary>
public record StatusFeedExt : OrderStatusExt
{
    /// <summary>
    /// The sequence number of the status feed 
    /// </summary>
    [JsonPropertyName("sequenceNumber")]
    public required int SequenceNumber { get; init; }
}
