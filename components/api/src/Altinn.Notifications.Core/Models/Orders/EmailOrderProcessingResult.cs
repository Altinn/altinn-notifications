using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the in-memory result of processing an email notification order.
/// Returned by the channel processing service instead of persisting eagerly;
/// handed to the repository layer for atomic persistence.
/// </summary>
/// <param name="EmailNotifications">The email notifications built during order processing.</param>
/// <param name="ExpirationDateTime">
/// The point in time after which unsent notifications may be discarded.
/// Standard processing paths set this to <c>requestedSendTime + 48h</c>, or to the expiry
/// calculated from the email send window for orders with the Daytime sending time policy;
/// <c>null</c> is only valid when all notifications are in a terminal state and
/// will never be picked up by the send pipeline. The repository falls back to
/// <c>DateTime.UtcNow</c> if not provided.
/// </param>
public sealed record EmailOrderProcessingResult(
    IReadOnlyList<EmailNotification> EmailNotifications,
    DateTime? ExpirationDateTime);
