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
    /// Registers the notification order sequence.
    /// </summary>
    /// <param name="orderRequest">The order request.</param>
    /// <param name="mainNotificationOrder">The main notification order.</param>
    /// <param name="reminders">The reminders.</param>
    /// <returns></returns>
    Task<NotificationOrderRequestResponse> RegisterNotificationOrderSequence(NotificationOrderChainRequest orderRequest, NotificationOrder mainNotificationOrder, List<NotificationOrder> reminders);
}
