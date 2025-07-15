using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Defines methods for persisting and retrieving instant notification orders.
/// </summary>
public interface IInstantOrderRepository
{
    /// <summary>
    /// Creates a new high-priority instant notification order in the database for immediate processing.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The instant notification order containing recipient and delivery information.
    /// </param>
    /// <param name="notificationOrder">
    /// The corresponding standard notification order that will be used for tracking and processing.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// The persisted <see cref="InstantNotificationOrder"/> with generated IDs and timestamp information.
    /// </returns>
    public Task<InstantNotificationOrder> Create(InstantNotificationOrder instantNotificationOrder, NotificationOrder notificationOrder, CancellationToken cancellationToken = default);

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
    /// A <see cref="Task{TResult}"/> containing a <see cref="InstantNotificationOrderTracking"/> with identifiers and sender references for the instant notification order,
    /// or <c>null</c> if no matching order is found for the provided parameters.
    /// </returns>
    Task<InstantNotificationOrderTracking?> GetInstantOrderTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);
}
