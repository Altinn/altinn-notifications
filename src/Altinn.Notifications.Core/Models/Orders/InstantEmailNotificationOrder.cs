using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents an instant email notification order that is processed immediately without waiting for scheduled processing.
/// </summary>
public record InstantEmailNotificationOrder
{
    /// <summary>
    /// The creator of the instant email notification order.
    /// </summary>
    public required Creator Creator { get; init; }

    /// <summary>
    /// The date and time for when the instant email notification order was created.
    /// </summary>
    public DateTime Created { get; init; }

    /// <summary>
    /// The unique identifier that is used to ensure the same notification order is not processed multiple times.
    /// </summary>
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// The unique identifier for the entire notification order chain.
    /// </summary>
    public required Guid OrderChainId { get; init; }

    /// <summary>
    /// The unique identifier for the instant email notification order.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// The email details including recipient email address and email content.
    /// </summary>
    public required InstantEmailDetails InstantEmailDetails { get; init; }

    /// <summary>
    /// The reference identifier assigned by the sender for tracking purposes.
    /// </summary>
    public string? SendersReference { get; init; }

    /// <summary>
    /// The type of the instant notification order.
    /// </summary>
    public OrderType Type { get; } = OrderType.Instant;
}
