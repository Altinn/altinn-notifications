using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Models.Status;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the combined in-memory result of processing a notification order,
/// covering all notification channels. Returned by the channel processing service
/// instead of persisting eagerly; handed to the repository layer as a single unit
/// of work to be persisted atomically within one database transaction.
/// </summary>
/// <param name="StatusToSet">The processing status the order should be moved to.</param>
/// <param name="EmailNotifications">Email notifications to persist. Empty if the order has no email channel.</param>
/// <param name="SmsNotifications">SMS notifications to persist. Empty if the order has no SMS channel.</param>
/// <param name="CompletesOrder">
/// Whether this processing result transitions the order to <see cref="OrderProcessingStatus.Completed"/>.
/// When <c>true</c>, <paramref name="StatusFeed"/> and <paramref name="NotificationLog"/> must be populated.
/// </param>
/// <param name="StatusFeed">
/// The status feed entry to persist alongside order completion.
/// Must be non-null when <paramref name="CompletesOrder"/> is <c>true</c>.
/// </param>
/// <param name="NotificationLog">
/// The notification log entry to persist alongside order completion.
/// Must be non-null when <paramref name="CompletesOrder"/> is <c>true</c>.
/// </param>
public sealed record OrderProcessingResult(
    OrderProcessingStatus StatusToSet,
    IReadOnlyList<EmailNotification> EmailNotifications,
    IReadOnlyList<SmsNotification> SmsNotifications,
    bool CompletesOrder,
    StatusFeed? StatusFeed,
    NotificationLogEntry? NotificationLog);
