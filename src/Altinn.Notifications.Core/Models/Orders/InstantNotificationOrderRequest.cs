using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents a request to send an SMS notification that bypasses the standard notification queue for immediate delivery.
/// </summary>
/// <remarks>
/// Unlike regular notifications that are queued for processing, instant notifications are sent immediately
/// through the SMS service's direct delivery channel. The order and SMS notification are created in a single transaction.
/// </remarks>
public record InstantNotificationOrderRequest
{
    /// <summary>
    /// A unique identifier used to ensure the same notification is not processed multiple times.
    /// </summary>
    /// <remarks>
    /// This value must be unique for each distinct notification attempt.
    /// If a request with the same idempotency ID is received multiple times,
    /// only the first one will be processed, and subsequent requests will return the original response.
    /// This prevents duplicate SMS messages being sent due to retries or network issues.
    /// </remarks>
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// A reference identifier assigned by the sender for tracking purposes.
    /// </summary>
    /// <remarks>
    /// This value can be used for correlating the notification with the sender's systems.
    /// It is included in response payloads and can be used for auditing or lookup operations.
    /// </remarks>
    public string? SendersReference { get; init; }

    /// <summary>
    /// The SMS recipient information and message content.
    /// </summary>
    /// <remarks>
    /// Contains the destination phone number, message content, time-to-live setting,
    /// and sender information. This data is forwarded directly to the SMS service's
    /// instant delivery endpoint after validation.
    /// </remarks>
    public required RecipientInstantSms RecipientSms { get; init; }
}
