using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents the outcome from creating a notification order.
/// </summary>
public class NotificationOrderResponseBaseContentExt
{
    /// <summary>
    /// Gets or sets the shipment identifier.
    /// </summary>
    [JsonPropertyOrder(1)]
    [JsonPropertyName("shipmentId")]
    public required Guid ShipmentId { get; set; }

    /// <summary>
    /// Gets or sets the sender's reference.
    /// </summary>
    /// <value>
    /// A reference used to identify the notification order in the sender's system.
    /// </value>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }
}
