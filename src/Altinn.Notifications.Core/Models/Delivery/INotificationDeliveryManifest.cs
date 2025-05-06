using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Status;

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
public interface INotificationDeliveryManifest : IStatus
{
    /// <summary>
    /// Gets the unique identifier for this shipment.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> that uniquely identifies this shipment within the notification system.
    /// </value>
    Guid ShipmentId { get; }

    /// <summary>
    /// Gets the sender-provided reference for cross-system correlation.
    /// </summary>
    /// <value>
    /// An optional string supplied by the sender to link this shipment to external systems
    /// or business processes, enabling easier cross-system tracing and reconciliation.
    /// </value>
    string? SendersReference { get; }

    /// <summary>
    /// Gets the classification or category of this shipment.
    /// </summary>
    /// <value>
    /// A string categorizing the shipment (e.g., "Notification" or "Reminder"),
    /// useful for filtering, reporting, or specialized processing workflows.
    /// </value>
    string Type { get; }

    /// <summary>
    /// Gets the collection of recipient-specific delivery tracking records.
    /// </summary>
    /// <value>
    /// An immutable list contains detailed delivery manifests for each recipient, including:
    /// - The destination address (e.g., email address or phone number)
    /// - The current delivery status
    /// - A description of the status, if available
    /// - The timestamp of the last status update
    /// </value>
    IImmutableList<IDeliveryManifest> Recipients { get; }
}
