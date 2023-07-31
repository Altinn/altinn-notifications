using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for operations related to notification orders
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Retrieves a notification order by id
    /// </summary>
    /// <param name="orderId">The order id</param>
    public Task<(NotificationOrder? Order, ServiceError? Error)> GetOrderById(Guid orderId);

    /// <summary>
    /// Retrieves a notification order by senders reference
    /// </summary>
    /// <param name="senderRef">The senders reference</param>
    public Task<(NotificationOrder? Order, ServiceError? Error)> GetOrderBySendersReference(string senderRef);
}