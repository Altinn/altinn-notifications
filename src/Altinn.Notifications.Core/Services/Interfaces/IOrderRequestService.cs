using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the notification order service
/// </summary>
public interface IOrderRequestService
{
    /// <summary>
    /// Registers a new order
    /// </summary>
    /// <param name="orderRequest">The notification order request</param>
    /// <returns>The order request response object</returns>
    Task<NotificationOrderRequestResponse> RegisterNotificationOrder(NotificationOrderRequest orderRequest);

    /// <summary>
    /// Registers a notification order chain with optional reminders for delayed delivery.
    /// </summary>
    /// <param name="orderRequest">
    /// The notification order chain request containing the primary notification details and any associated reminders with their delivery schedules.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// On success, a <see cref="Task{TResult}"/> containing a <see cref="NotificationOrderChainResponse"/> with 
    /// the generated order chain identifier and receipt information for both the main notification and any associated reminders.
    /// On failure, a <see cref="ServiceError"/> indicating the reason for the failure.
    /// </returns>
    /// <remarks>
    /// This method processes a chain of notifications, consisting of an initial notification and
    /// optional follow-up reminders. It handles template configuration, recipient lookup, and
    /// validation before storing the entire sequence for processing.
    /// 
    /// The operation is idempotent when using the same <see cref="NotificationOrderChainRequest.IdempotencyId"/>,
    /// ensuring that repeated calls with identical parameters won't create duplicate notification chains.
    /// 
    /// When reminders are specified, they will be scheduled for delivery after the main notification
    /// according to their respective <see cref="NotificationReminder.DelayDays"/> value.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<Result<NotificationOrderChainResponse, ServiceError>> RegisterNotificationOrderChain(NotificationOrderChainRequest orderRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a notification order chain with optional reminders for delayed delivery.
    /// </summary>
    /// <param name="orderRequest">
    /// The notification order chain request containing the primary notification details and any associated reminders with their delivery schedules.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// On success, a <see cref="Task{TResult}"/> containing a <see cref="NotificationOrderChainResponse"/> with 
    /// the generated order chain identifier and receipt information for both the main notification and any associated reminders.
    /// On failure, a <see cref="ServiceError"/> indicating the reason for the failure.
    /// </returns>
    /// <remarks>
    /// This method processes a chain of notifications, consisting of an initial notification and
    /// optional follow-up reminders. It handles template configuration, recipient lookup, and
    /// validation before storing the entire sequence for processing.
    /// 
    /// The operation is idempotent when using the same <see cref="NotificationOrderChainRequest.IdempotencyId"/>,
    /// ensuring that repeated calls with identical parameters won't create duplicate notification chains.
    /// 
    /// When reminders are specified, they will be scheduled for delivery after the main notification
    /// according to their respective <see cref="NotificationReminder.DelayDays"/> value.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<Result<InstantNotificationOrderResponse, ServiceError>> RegisterNotificationOrderChain(InstantNotificationOrderRequest orderRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tracking information for a notification order chain using the creator's name and idempotency identifier.
    /// </summary>
    /// <param name="creatorName">
    /// The short name of the creator that originally submitted the notification order chain.
    /// </param>
    /// <param name="idempotencyId">
    /// The idempotency identifier that was defined when the order chain was created.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="NotificationOrderChainResponse"/> with 
    /// identifiers and sender references for both the order chain and its components, or 
    /// <c>null</c> if no matching order chain is found with the provided parameters.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled through the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<NotificationOrderChainResponse?> RetrieveOrderChainTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);

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
    /// A <see cref="Task{TResult}"/> containing an <see cref="InstantNotificationOrderResponse"/> with identifiers and sender references for the instant notification order,
    /// or <c>null</c> if no matching order is found for the provided parameters.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the provided <paramref name="cancellationToken"/>.
    /// </exception>
    Task<InstantNotificationOrderResponse?> RetrieveInstanOrderTracking(string creatorName, string idempotencyId, CancellationToken cancellationToken = default);
}
