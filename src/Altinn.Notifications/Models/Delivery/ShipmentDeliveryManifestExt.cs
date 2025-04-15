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
/// hierarchy. By implementing <see cref="IStatusExt"/>, it enables standardized status monitoring at the shipment level
/// while orchestrating the delivery workflow across multiple recipients.
/// </para>
/// <para>
/// This entity aggregates and consolidates tracking data from individual recipient-specific deliveries 
/// (represented by <see cref="IDeliverableEntityExt"/> implementations) while maintaining its own status information.
/// This design enables both high-level shipment monitoring and granular per-recipient tracking in a unified model.
/// </para>
/// <para>
/// Within the notification workflow, a single shipment typically represents a logical notification 
/// that may target multiple recipients using various channels (SMS or email). The manifest maintains 
/// the relationship between these individual deliveries and the overall shipment status.
/// </para>
/// </remarks>
public record ShipmentDeliveryManifestExt : IStatusExt
{
    /// <summary>
    /// Gets the unique identifier for this shipment.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> that uniquely identifies this shipment within the notification system.
    /// This identifier serves as the primary key for shipment tracking and correlation.
    /// </value>
    [JsonPropertyName("shipmentId")]
    public Guid ShipmentId { get; init; }

    /// <summary>
    /// Gets the sender-provided reference for cross-system correlation.
    /// </summary>
    /// <value>
    /// An optional string supplied by the sender to link this shipment to external systems
    /// or business processes, enabling easier cross-system tracing and reconciliation.
    /// May be null if no external reference was provided.
    /// </value>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; init; }

    /// <summary>
    /// Gets the classification or category of this shipment.
    /// </summary>
    /// <value>
    /// A string categorizing the shipment (e.g., "Notification" or "Reminder"),
    /// useful for filtering, reporting, or specialized processing workflows.
    /// </value>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("description")]
    public string? StatusDescription { get; init; } = null;

    /// <inheritdoc />
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the collection of recipient-specific delivery tracking records.
    /// </summary>
    /// <value>
    /// An immutable list of <see cref="IDeliverableEntityExt"/> instances, each representing
    /// a delivery manifest to a specific recipient through a particular communication channel.
    /// </value>
    [JsonPropertyName("recipients")]
    public ImmutableList<IDeliverableEntityExt> Recipients { get; init; } = [];
}
