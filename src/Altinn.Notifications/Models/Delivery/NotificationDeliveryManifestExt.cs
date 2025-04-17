using System.Collections.Immutable;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents the root tracking entity for a notification shipment that can be sent to multiple recipients through various communication channels.
/// </summary>
/// <remarks>
/// This entity aggregates tracking data from individual recipient-specific deliveries
/// (represented by <see cref="IDeliveryManifestExt"/>), while also maintaining its own status information.
/// This design enables both high-level shipment monitoring and granular per-recipient tracking in a unified model.
/// 
/// A shipment typically represents a single logical notification, which may target multiple recipients. The <see cref="NotificationDeliveryManifestExt"/>
/// maintains the relationship between these individual deliveries and the overall shipment status, supporting the tracking and management of notifications 
/// across multiple recipients and communication channels.
/// </remarks>
public record NotificationDeliveryManifestExt : INotificationDeliveryManifestExt
{
    /// <inheritdoc />
    [JsonPropertyName("shipmentId")]
    public required Guid ShipmentId { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("type")]
    public required string Type { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("status")]
    public required string Status { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("description")]
    public string? StatusDescription { get; init; } = null;

    /// <inheritdoc />
    [JsonPropertyName("lastUpdate")]
    public required DateTime LastUpdate { get; init; } = DateTime.UtcNow;

    /// <inheritdoc />
    [JsonPropertyName("recipients")]
    public required IImmutableList<IDeliveryManifestExt> Recipients { get; init; } = [];
}
