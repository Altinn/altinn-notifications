using System.Collections.Immutable;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.Status;

namespace Altinn.Notifications.Models.Status;

/// <summary>
/// Represents the status of an order when the status feed entry was inserted. <seealso cref="StatusFeed"/>
/// </summary>
public record OrderStatusExt
{
    /// <summary>
    /// Gets the unique identifier for the shipment associated with this order status.
    /// </summary>
    [JsonPropertyName("shipmentId")]
    public Guid ShipmentId { get; init; }

    /// <summary>
    /// Gets the sender's reference for the order, as provided by the client system.
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <summary>
    /// Gets the list of recipients and their delivery status information for this order.
    /// </summary>
    [JsonPropertyName("recipients")]
    public required IImmutableList<StatusFeedRecipientExt> Recipients { get; init; }

    /// <summary>
    /// Gets the current lifecycle status of the order.
    /// </summary>
    [JsonPropertyName("status")]
    public ProcessingLifecycleExt Status { get; init; }

    /// <summary>
    /// Gets the date and time when the status was was created.
    /// </summary>
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdated { get; init; }

    /// <summary>
    /// The type of shipment (e.g. Notificaion or Reminder)
    /// </summary>
    [JsonPropertyName("shipmentType")]
    public string? ShipmentType { get; init; }
}
