using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines operations for registering and tracking instant notification orders.
/// </summary>
public interface IInstantOrderRequestService
{
    /// <summary>
    /// Registers an instant notification order for immediate processing and delivery.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The instant notification order containing recipient and message details to be processed immediately.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="Result{TValue, TError}"/>.
    /// On success, the result contains a <see cref="NotificationOrder"/> with details about the registered instant notification order.
    /// On failure, the result contains a <see cref="ServiceError"/> describing the reason for failure.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<Result<NotificationOrder, ServiceError>> RegisterInstantOrder(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tracking information for an instant notification order using the creator's name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the creator that originally submitted the instant notification order.
    /// </param>
    /// <param name="idempotencyId">
    /// The idempotency identifier that was defined when the instant notification order was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing an <see cref="InstantNotificationOrderTracking"/> object with tracking details for the instant notification order,
    /// or <c>null</c> if no matching order is found with the provided parameters.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<InstantNotificationOrderTracking?> RetrieveInstantOrderTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);
}
