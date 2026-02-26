using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents an instant SMS notification order that is processed immediately without waiting for scheduled processing.
/// </summary>
public record InstantSmsNotificationOrder
{
    /// <summary>
    /// The creator of the instant SMS notification order.
    /// </summary>
    public required Creator Creator { get; init; }

    /// <summary>
    /// The date and time for when the instant SMS notification order was created.
    /// </summary>
    public required DateTime Created { get; init; }

    /// <summary>
    /// The unique identifier that is used to ensure the same notification order is not processed multiple times.
    /// </summary>
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// The unique identifier for the entire notification order chain.
    /// </summary>
    public required Guid OrderChainId { get; init; }

    /// <summary>
    /// The unique identifier for the instant SMS notification order.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// The SMS delivery details including recipient phone number, time-to-live, and message content.
    /// </summary>
    public required ShortMessageDeliveryDetails ShortMessageDeliveryDetails { get; init; }

    /// <summary>
    /// The reference identifier assigned by the sender for tracking purposes.
    /// </summary>
    public string? SendersReference { get; init; }

    /// <summary>
    /// The type of the instant notification order.
    /// </summary>
    public OrderType Type { get; } = OrderType.Instant;
}
