namespace Altinn.Notifications.Core.Models.Notification;

/// <summary>
/// Wraps an in-memory <see cref="SmsNotification"/> together with the persistence-only
/// metadata required by the repository insert: expiry time and segment count.
/// Only used on the order-processing → repository write path.
/// </summary>
public sealed record PendingSmsNotification(
    SmsNotification Notification,
    DateTime ExpiryTime,
    int MessageSegmentCount);
