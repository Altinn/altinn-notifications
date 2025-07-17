using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Orders;

/// <summary>
/// Represents the response model returned after processing a request to send an instant notification to a single recipient.
/// </summary>
public record InstantNotificationOrderResponseExt
{
    /// <summary>
    /// The unique identifier for the notification order chain.
    /// </summary>
    /// <remarks>
    /// This identifier can be used to reference the entire notification order chain in subsequent operations
    /// or for tracking purposes.
    /// </remarks>
    [JsonPropertyName("notificationOrderId")]
    public required Guid OrderChainId { get; init; }

    /// <summary>
    /// The unique identifier for notification order.
    /// </summary>
    [JsonPropertyName("notification")]
    public required NotificationOrderChainShipmentExt Notification { get; init; }
}
