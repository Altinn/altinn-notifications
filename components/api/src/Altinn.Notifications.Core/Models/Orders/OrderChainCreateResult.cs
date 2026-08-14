namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the result of attempting to create an order chain in the database.
/// </summary>
/// <remarks>
/// This type is returned by the repository when inserting an order chain with idempotency handling.
/// It always contains the tracking data needed to build a <see cref="NotificationOrderChainResponse"/>,
/// regardless of whether the chain was newly inserted or already existed.
/// </remarks>
public class OrderChainCreateResult
{
    /// <summary>
    /// Gets a value indicating whether the order chain was newly created.
    /// When <c>false</c>, a concurrent request with the same idempotency key already committed the chain.
    /// </summary>
    public required bool IsNewlyCreated { get; init; }

    /// <summary>
    /// Gets the internal database identifier for the order chain row.
    /// </summary>
    public long InternalId { get; init; }

    /// <summary>
    /// Gets the unique identifier for the notification order chain.
    /// </summary>
    public required Guid OrderChainId { get; init; }

    /// <summary>
    /// Gets the unique identifier for the main notification order (shipment).
    /// </summary>
    public required Guid ShipmentId { get; init; }

    /// <summary>
    /// Gets the sender's reference for the main notification order.
    /// </summary>
    public string? SendersReference { get; init; }

    /// <summary>
    /// Gets the reminder shipments associated with the order chain.
    /// </summary>
    public List<NotificationOrderChainShipment>? Reminders { get; init; }
}
