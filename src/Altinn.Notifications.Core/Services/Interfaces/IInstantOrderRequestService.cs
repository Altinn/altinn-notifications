using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides methods for registering and tracking instant notification orders.
/// </summary>
public interface IInstantOrderRequestService
{
    /// <summary>
    /// Retrieves tracking details for an instant notification order using the creator's short name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the entity that submitted the instant notification order.
    /// </param>
    /// <param name="idempotencyId">
    /// The unique idempotency identifier assigned when the order was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing an <see cref="InstantNotificationOrderTracking"/> instance if a matching order is found;
    /// otherwise, <c>null</c> if no matching order exists.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    Task<InstantNotificationOrderTracking?> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new instant SMS notification order (legacy method for backward compatibility).
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing recipient and message details.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing an <see cref="InstantNotificationOrderTracking"/> instance with tracking information if registration succeeds;
    /// otherwise, <c>null</c> if registration fails.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    [Obsolete("This method is deprecated. Use PersistInstantSmsNotificationAsync(InstantSmsNotificationOrder) instead.")]
    Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new instant SMS notification order.
    /// </summary>
    /// <param name="instantSmsNotificationOrder">
    /// The <see cref="InstantSmsNotificationOrder"/> containing SMS delivery details.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing an <see cref="InstantNotificationOrderTracking"/> instance with tracking information if registration succeeds;
    /// otherwise, <c>null</c> if registration fails.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantSmsNotificationOrder instantSmsNotificationOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new instant email notification order.
    /// </summary>
    /// <param name="instantEmailNotificationOrder">
    /// The <see cref="InstantEmailNotificationOrder"/> containing email delivery details.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing an <see cref="InstantNotificationOrderTracking"/> instance with tracking information if registration succeeds;
    /// otherwise, <c>null</c> if registration fails.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    Task<InstantNotificationOrderTracking?> PersistInstantEmailNotificationAsync(InstantEmailNotificationOrder instantEmailNotificationOrder, CancellationToken cancellationToken = default);
}
