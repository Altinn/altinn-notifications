using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides methods for registering and tracking composed email notification orders.
/// </summary>
public interface IComposedEmailOrderRequestService
{
    /// <summary>
    /// Retrieves tracking information for a composed email order chain using the creator's name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the creator that originally submitted the composed email order chain.
    /// </param>
    /// <param name="idempotencyId">
    /// The idempotency identifier that was defined when the order chain was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="NotificationOrderChainResponse"/> with
    /// identifiers and sender reference for the composed email order chain, or
    /// <c>null</c> if no matching order chain is found with the provided parameters.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<NotificationOrderChainResponse?> RetrieveOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new composed email order chain in the database.
    /// </summary>
    /// <param name="orderRequest">
    /// The <see cref="NotificationOrderChainRequest"/> containing recipient, sending options, and SAS-referenced files.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="Result{T}"/> with a <see cref="NotificationOrderChainResponse"/>
    /// on success, or a <see cref="ProblemInstance"/> if the request is invalid.
    /// </returns>
    /// <remarks>
    /// Composed email orders do not support reminders. The returned receipt always has
    /// <see cref="NotificationOrderChainReceipt.Reminders"/> set to <c>null</c>.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<Result<NotificationOrderChainResponse>> RegisterComposedEmailOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default);
}
