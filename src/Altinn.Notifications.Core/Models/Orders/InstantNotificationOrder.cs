using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents a request to send an SMS notification that bypasses the standard notification queue for immediate delivery.
/// </summary>
/// <remarks>
/// Unlike regular notifications that are queued for processing, instant notifications are sent immediately
/// through the SMS service's direct delivery channel. The order and SMS notification are created in a single transaction.
/// </remarks>
public record InstantNotificationOrder
{
    /// <summary>
    /// The type of the instant notification order request.
    /// </summary>
    /// <remarks>
    /// This value is always set to <see cref="OrderType.Instant"/> to indicate this is an immediate delivery request.
    /// </remarks>
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
    /// <remarks>
    /// This value must be unique for each distinct notification attempt.
    /// If a request with the same idempotency identifier is received multiple times,
    /// only the first one will be processed, and subsequent requests will return the original response.
    /// This prevents duplicate SMS messages being sent due to retries or network issues.
    /// </remarks>
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// The reference identifier assigned by the sender for tracking purposes.
    /// </summary>
    /// <remarks>
    /// This value can be used for correlating the notification with the sender's systems.
    /// It is included in response payloads and can be used for auditing or lookup operations.
    /// </remarks>
    public string? SendersReference { get; init; }

    /// <summary>
    /// The creator of the instant notification order request.
    /// </summary>
    public required Creator Creator { get; init; }

    /// <summary>
    /// The recipient information and message content.
    /// </summary>
    /// <remarks>
    /// Contains the destination phone number, message content, time-to-live setting,
    /// and sender information. This data is forwarded directly to the SMS service's
    /// instant delivery endpoint after validation.
    /// </remarks>
    public required InstantNotificationRecipient Recipient { get; init; }
}
