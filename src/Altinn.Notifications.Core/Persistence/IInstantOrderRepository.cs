using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Defines methods for persisting and retrieving instant notification orders.
/// </summary>
public interface IInstantOrderRepository
{
    /// <summary>
    /// Retrieves tracking information for an instant notification order using the creator's name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the creator who originally submitted the instant notification order.
    /// </param>
    /// <param name="idempotencyId">
    /// The idempotency identifier specified when the instant notification order was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="InstantNotificationOrderTracking"/> with tracking information,
    /// or <c>null</c> if no matching order is found for the provided parameters.
    /// </returns>
    Task<InstantNotificationOrderTracking?> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new high-priority instant notification order.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing recipient, message, and delivery details.
    /// </param>
    /// <param name="notificationOrder">
    /// The <see cref="NotificationOrder"/> representing the standard notification order.
    /// </param>
    /// <param name="smsNotification">
    /// The <see cref="SmsNotification"/> instance containing SMS-specific delivery information.
    /// </param>
    /// <param name="smsExpiryTime">
    /// The <see cref="DateTime"/> indicating when the SMS notification expires and should no longer be delivered.
    /// </param>
    /// <param name="smsMessageCount">
    /// The number of SMS messages to be sent based on the message content.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="InstantNotificationOrderTracking"/> with tracking information,
    /// or <c>null</c> if the operation failed.
    /// </returns>
    Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, NotificationOrder notificationOrder, SmsNotification smsNotification, DateTime smsExpiryTime, int smsMessageCount, CancellationToken cancellationToken = default);
}
