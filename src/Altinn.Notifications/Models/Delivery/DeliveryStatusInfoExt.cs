using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Provides a base implementation for tracking the status and destination information of deliverable entities 
/// within the notification system.
/// </summary>
/// <remarks>
/// This abstract record implements the <see cref="IDeliveryStatusInfoExt"/> interface, providing a standardized
/// foundation for tracking the status and destination of notifications across various delivery channels.
///
/// It combines destination address information with status tracking, ensuring a consistent implementation pattern
/// for all delivery types. The class centralizes key properties, including the destination address, the current status,
/// a detailed status description, and the timestamp of the last update.
///
/// Specialized implementations can extend this class to support channel-specific tracking, while inheriting its core 
/// functionality for status and destination management.
/// </remarks>
public abstract record DeliveryStatusInfoExt : IDeliveryStatusInfoExt
{
    /// <inheritdoc />
    [JsonPropertyName("destination")]
    public required string Destination { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("status")]
    public required string Status { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("description")]
    public string? StatusDescription { get; init; } = null;

    /// <inheritdoc />
    [JsonPropertyName("lastUpdate")]
    public required DateTime LastUpdate { get; init; } = DateTime.UtcNow;
}
