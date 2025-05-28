using System.Collections.Immutable;
using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Status;

/// <summary>
/// Represents the status of an order when the status feed entry was inserted. <seealso cref="StatusFeed"/>
/// </summary>
public record OrderStatus
{
    /// <summary>
    /// Gets the unique identifier for the shipment associated with this order status.
    /// </summary>
    public Guid ShipmentId { get; init; }

    /// <summary>
    /// Gets the sender's reference for the order, as provided by the client system.
    /// </summary>
    public string? SendersReference { get; init; }

    /// <summary>
    /// Gets the type of status represented by this entry (e.g., order, SMS, or email).
    /// </summary>
    public string? ShipmentType { get; init; }

    /// <summary>
    /// Gets the list of recipients and their delivery status information for this order.
    /// </summary>
    public required IImmutableList<Recipient> Recipients { get; init; }

    /// <summary>
    /// Gets the current lifecycle status of the order.
    /// </summary>
    public ProcessingLifecycle Status { get; init; }

    /// <summary>
    /// Gets the date and time when the status was last updated.
    /// </summary>
    public DateTime LastUpdated { get; init; }
}
