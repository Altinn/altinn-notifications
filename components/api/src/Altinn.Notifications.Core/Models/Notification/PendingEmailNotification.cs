namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Wraps an in-memory <see cref="EmailNotification"/> together with the persistence-only
/// metadata required by the repository insert: expiry time.
/// Only used on the order-processing → repository write path.
/// </summary>
public sealed record PendingEmailNotification(
    EmailNotification Notification,
    DateTime ExpiryTime);
