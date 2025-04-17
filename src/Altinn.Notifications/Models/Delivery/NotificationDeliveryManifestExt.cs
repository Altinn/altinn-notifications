using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents the root tracking entity for a notification shipment that
/// can be sent to multiple recipients through various communication channels.
/// </summary>
/// <remarks>
/// This entity aggregates tracking data from individual recipient-specific deliveries
/// (represented by <see cref="IDeliveryManifestExt"/>), while also maintaining its own status information.
/// This design enables both high-level shipment monitoring and granular per-recipient tracking in a unified model.
/// 
/// A shipment typically represents a single logical notification, which may target multiple recipients.
/// </remarks>
public record NotificationDeliveryManifestExt : INotificationDeliveryManifestExt
{
    /// <inheritdoc />
    [JsonPropertyName("sequenceNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SequenceNumber { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("shipmentId")]
    public required Guid ShipmentId { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusDescription { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("lastUpdate")]
    public required DateTime LastUpdate { get; init; }

    /// <inheritdoc />
    [JsonPropertyName("recipients")]
    public required IImmutableList<IDeliveryManifestExt> Recipients { get; init; }
}
