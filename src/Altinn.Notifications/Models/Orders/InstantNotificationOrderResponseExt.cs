using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Orders;

/// <summary>
/// Represents the response model for a request to send an SMS notification immediately.
/// </summary>
public class InstantNotificationOrderResponseExt
{
    /// <summary>
    /// Gets or sets the unique identifier for the notification order chain.
    /// </summary>
    /// <remarks>
    /// This identifier can be used to reference the entire notification order chain in subsequent operations
    /// or for tracking purposes.
    /// </remarks>
    [JsonPropertyName("notificationOrderId")]
    public required Guid OrderChainId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for notification order.
    /// </summary>
    [JsonPropertyName("notification")]
    public required NotificationOrderChainShipmentExt Notification { get; set; }
}
