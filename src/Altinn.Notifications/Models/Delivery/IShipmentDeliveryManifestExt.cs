using System.Collections.Immutable;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents the root tracking interface for a notification shipment that can be sent to multiple recipients through various communication channels.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IShipmentDeliveryManifestExt"/> interface serves as the primary tracking contract in the notification system's
/// hierarchy. By extending <see cref="IStatusExt"/>, it enables standardized status monitoring at the shipment level
/// while orchestrating the delivery workflow across multiple recipients.
/// </para>
/// <para>
/// This interface defines properties to aggregate and consolidate tracking data from individual recipient-specific deliveries 
/// (represented by <see cref="IDeliveryStatusInfoExt"/> implementations) while maintaining its own status information.
/// This design enables both high-level shipment monitoring and granular per-recipient tracking in a unified model.
/// </para>
/// <para>
/// Within the notification workflow, a single shipment typically represents a logical notification 
/// that may target multiple recipients using various channels (SMS or email). The interface defines properties to maintain 
/// the relationship between these individual deliveries and the overall shipment status.
/// </para>
/// </remarks>
public interface IShipmentDeliveryManifestExt : IStatusExt
{
    /// <summary>
    /// Gets the unique identifier for this shipment.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> that uniquely identifies this shipment within the notification system.
    /// This identifier serves as the primary key for shipment tracking and correlation.
    /// </value>
    [JsonPropertyName("shipmentId")]
    Guid ShipmentId { get; }

    /// <summary>
    /// Gets the sender-provided reference for cross-system correlation.
    /// </summary>
    /// <value>
    /// An optional string supplied by the sender to link this shipment to external systems
    /// or business processes, enabling easier cross-system tracing and reconciliation.
    /// May be null if no external reference was provided.
    /// </value>
    [JsonPropertyName("sendersReference")]
    string? SendersReference { get; }

    /// <summary>
    /// Gets the classification or category of this shipment.
    /// </summary>
    /// <value>
    /// A string categorizing the shipment (e.g., "Notification" or "Reminder"),
    /// useful for filtering, reporting, or specialized processing workflows.
    /// </value>
    [JsonPropertyName("type")]
    string Type { get; }

    /// <summary>
    /// Gets the collection of recipient-specific delivery tracking records.
    /// </summary>
    /// <value>
    /// An immutable list of <see cref="IDeliveryStatusInfoExt"/> instances, each representing
    /// a delivery manifest to a specific recipient through a particular communication channel.
    /// </value>
    [JsonPropertyName("recipients")]
    IImmutableList<IDeliveryStatusInfoExt> Recipients { get; }
}
