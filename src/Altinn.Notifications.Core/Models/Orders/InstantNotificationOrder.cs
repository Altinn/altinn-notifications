using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents an instant notification order that is processed immediately without waiting for scheduled processing.
/// </summary>
public record InstantNotificationOrder
{
    /// <summary>
    /// The type of the instant notification order.
    /// </summary>
    public OrderType Type { get; } = OrderType.Instant;

    /// <summary>
    /// The unique identifier for the instant notification order.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// The unique identifier for the entire notification order chain.
    /// </summary>
    public required Guid OrderChainId { get; init; }

    /// <summary>
    /// The unique identifier that is used to ensure the same notification order is not processed multiple times.
    /// </summary>
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// The reference identifier assigned by the sender for tracking purposes.
    /// </summary>
    public string? SendersReference { get; init; }

    /// <summary>
    /// The creator of the instant notification order.
    /// </summary>
    public required Creator Creator { get; init; }

    /// <summary>
    /// The recipient information and message content.
    /// </summary>
    public required InstantNotificationRecipient Recipient { get; init; }
}
