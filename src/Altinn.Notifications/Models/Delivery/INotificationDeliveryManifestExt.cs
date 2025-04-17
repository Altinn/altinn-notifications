using System.Collections.Immutable;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Models.Delivery;

/// <summary>
/// Represents the delivery manifest for a notification shipment.
/// </summary>
/// <remarks>
/// This interface defines the structure of a shipment that delivers a notification. Each shipment is uniquely identified
/// by a <see cref="ShipmentId"/> and may optionally include a <see cref="SendersReference"/> to correlate the shipment
/// with external systems or processes. The <see cref="Type"/> is always "Notification", indicating that the shipment
/// pertains to the delivery of a notification message.
///
/// The <see cref="Recipients"/> property contains detailed delivery manifests for each recipient, including:
/// - Their destination address (e.g., email address or phone number)
/// - The current delivery status
/// - A description of the status, if available
/// - The timestamp of the last status update
/// </remarks>
public interface INotificationDeliveryManifestExt : IStatusExt
{
    /// <summary>
    /// Gets the unique identifier for this shipment.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> that uniquely identifies this shipment within the notification system.
    /// </value>
    [JsonPropertyName("shipmentId")]
    Guid ShipmentId { get; }

    /// <summary>
    /// Gets the sender-provided reference for cross-system correlation.
    /// </summary>
    /// <value>
    /// An optional string supplied by the sender to link this shipment to external systems
    /// or business processes, enabling easier cross-system tracing and reconciliation.
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
    /// An immutable list of <see cref="IDeliveryManifestExt"/> instances, each representing
    /// a delivery manifest to a specific recipient through a particular communication channel.
    /// </value>
    [JsonPropertyName("recipients")]
    IImmutableList<IDeliveryManifestExt> Recipients { get; }
}
