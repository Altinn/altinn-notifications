using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Orders;

/// <summary>
/// Represents the response model for a request to send an SMS notification immediately.
/// </summary>
public class InstantNotificationOrderResponseExt
{
    /// <summary>
    /// Gets or sets the unique identifier for notification order.
    /// </summary>
    [JsonPropertyName("shipmentId")]
    public required Guid ShipmentId { get; set; }

    /// <summary>
    /// Gets or sets the sender's reference identifier.
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }
}
