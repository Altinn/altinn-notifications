using System.Collections.Immutable;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Delivery;

/// <summary>
/// Represents the root tracking entity for a notification shipment that
/// can be sent to multiple recipients through various communication channels.
/// </summary>
/// <remarks>
/// This entity aggregates tracking data from individual recipient-specific deliveries
/// (represented by <see cref="IDeliveryManifest"/>), while also maintaining its own status information.
/// This design enables both high-level shipment monitoring and granular per-recipient tracking in a unified model.
/// 
/// A shipment typically represents a single logical notification, which may target multiple recipients.
/// </remarks>
public record NotificationDeliveryManifest : INotificationDeliveryManifest
{
    /// <inheritdoc />
    public int? SequenceNumber { get; init; }

    /// <inheritdoc />
    public required Guid ShipmentId { get; init; }

    /// <inheritdoc />
    public string? SendersReference { get; init; }

    /// <inheritdoc />
    public required string Type { get; init; }

    /// <inheritdoc />
    public required ProcessingLifecycle Status { get; init; }

    /// <inheritdoc />
    public required DateTime LastUpdate { get; init; }

    /// <inheritdoc />
    public required IImmutableList<IDeliveryManifest> Recipients { get; init; }
}
