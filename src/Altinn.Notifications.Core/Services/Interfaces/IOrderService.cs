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
    /// <param name="id">The order id</param>
    /// <param name="creator">The creator of the orders</param>
    public Task<(NotificationOrder? Order, ServiceError? Error)> GetOrderById(Guid id, string creator);

    /// <summary>
    /// Retrieves a notification order by senders reference
    /// </summary>
    /// <param name="senderRef">The senders reference</param>
    /// <param name="creator">The creator of the orders</param>
    public Task<(List<NotificationOrder> Orders, ServiceError? Error)> GetOrdersBySendersReference(string senderRef, string creator);
}