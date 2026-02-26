using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for operations related to retrieving notification orders
/// </summary>
public interface IGetOrderService
{
    /// <summary>
    /// Retrieves a notification order by id
    /// </summary>
    /// <param name="id">The order id</param>
    /// <param name="creator">The creator of the orders</param>
    public Task<Result<NotificationOrder, ServiceError>> GetOrderById(Guid id, string creator);

    /// <summary>
    /// Retrieves a notification order by senders reference
    /// </summary>
    /// <param name="senderRef">The senders reference</param>
    /// <param name="creator">The creator of the orders</param>
    public Task<Result<List<NotificationOrder>, ServiceError>> GetOrdersBySendersReference(string senderRef, string creator);

    /// <summary>
    /// Retrieves a notification order with process and notification status by id
    /// </summary>
    /// <param name="id">The order id</param>
    /// <param name="creator">The creator of the orders</param>
    public Task<Result<NotificationOrderWithStatus, ServiceError>> GetOrderWithStatuById(Guid id, string creator);
}
