using Altinn.Notifications.Core.Models.Orders;
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
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing a <see cref="NotificationOrderRequestResponse"/> with 
    /// the generated order ID and any lookup results for recipients whose contact information
    /// needed resolution.
    /// </returns>
    /// <remarks>
    /// This method processes a chain of notifications, consisting of an initial notification and
    /// optional follow-up reminders. It handles template configuration, recipient lookup, and
    /// validation before storing the entire sequence for processing.
    /// </remarks>
    Task<NotificationOrderChainResponse> RegisterNotificationOrderChain(NotificationOrderChainRequest orderRequest);
}
