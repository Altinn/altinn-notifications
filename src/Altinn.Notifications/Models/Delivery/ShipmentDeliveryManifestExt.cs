using System.Collections.Immutable;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents the root tracking entity for a notification shipment that can be sent to multiple recipients through various communication channels.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ShipmentDeliveryManifestExt"/> record serves as the primary tracking entity in the notification system's
/// hierarchy. By implementing <see cref="IShipmentDeliveryManifestExt"/> (which extends <see cref="IStatusExt"/>), 
/// it enables standardized status monitoring at the shipment level while orchestrating the delivery workflow across multiple recipients.
/// </para>
/// <para>
/// This entity aggregates and consolidates tracking data from individual recipient-specific deliveries 
/// (represented by <see cref="IDeliveryStatusExt"/> implementations) while maintaining its own status information.
/// This design enables both high-level shipment monitoring and granular per-recipient tracking in a unified model.
/// </para>
/// <para>
/// Within the notification workflow, a single shipment typically represents a logical notification 
/// that may target multiple recipients using various channels (SMS or email). The manifest maintains 
/// the relationship between these individual deliveries and the overall shipment status.
/// </para>
/// </remarks>
public record ShipmentDeliveryManifestExt : IShipmentDeliveryManifestExt
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
    public required IImmutableList<IDeliveryStatusExt> Recipients { get; init; } = [];
}
