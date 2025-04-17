﻿using System.Collections.Immutable;

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
    public int? SequenceNumber { get; init; } = null;

    /// <inheritdoc />
    public required Guid ShipmentId { get; init; }

    /// <inheritdoc />
    public string? SendersReference { get; init; } = null;

    /// <inheritdoc />
    public required string Type { get; init; }

    /// <inheritdoc />
    public required string Status { get; init; }

    /// <inheritdoc />
    public string? StatusDescription { get; init; } = null;

    /// <inheritdoc />
    public required DateTime LastUpdate { get; init; }

    /// <inheritdoc />
    public required IImmutableList<IDeliveryManifest> Recipients { get; init; }
}
