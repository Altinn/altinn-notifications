using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Delivery;

/// <summary>
/// Provides a base implementation for tracking the status and destination information of deliverable entities 
/// within the notification system.
/// </summary>
/// <remarks>
/// This abstract record implements the <see cref="IDeliveryManifest"/> interface, providing a standardized
/// foundation for tracking the status and destination of notifications across various delivery channels.
///
/// It combines destination address information with status tracking, ensuring a consistent implementation pattern
/// for all delivery types. The class centralizes key properties, including the destination address, the current status, and the timestamp of the last update.
///
/// Specialized implementations can extend this class to support channel-specific tracking, while inheriting its core 
/// functionality for status and destination management.
/// </remarks>
public abstract record DeliveryManifest : IDeliveryManifest
{
    /// <inheritdoc />
    public required string Destination { get; init; }

    /// <inheritdoc />
    public required ProcessingLifecycle Status { get; init; }

    /// <inheritdoc />
    public required DateTime LastUpdate { get; init; }
}
