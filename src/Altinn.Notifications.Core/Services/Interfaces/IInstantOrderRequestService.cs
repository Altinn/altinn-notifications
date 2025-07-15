using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides methods for registering and tracking instant notification orders, enabling immediate delivery and status retrieval.
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
    /// The unique idempotency identifier assigned when the order was created, used to prevent duplicate processing.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="Result{InstantNotificationOrderTracking, ServiceError}"/>:
    /// <list type="table">
    /// <item>
    /// <description><see cref="InstantNotificationOrderTracking"/> if a matching order is found.</description>
    /// </item>
    /// <item>
    /// <description><see cref="ServiceError"/> if no matching order exists or an error occurs.</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    Task<Result<InstantNotificationOrderTracking, ServiceError>> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new instant notification order for immediate processing and delivery.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing recipient and message details for urgent delivery.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="Result{InstantNotificationOrderTracking, ServiceError}"/>:
    /// <list type="bullet">
    /// <item>
    /// <description><see cref="InstantNotificationOrderTracking"/> with tracking information if registration succeeds.</description>
    /// </item>
    /// <item>
    /// <description><see cref="ServiceError"/> if an error occurs.</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
    /// </exception>
    Task<Result<InstantNotificationOrderTracking, ServiceError>> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default);
}
