using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Repository.Interfaces;

/// <summary>
/// Interface describing all repository actions for notification orders
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Creates a new notification order in the database
    /// </summary>
    /// <param name="order">The order to save</param>
    /// <returns>The saved notification order</returns>
    public Task<NotificationOrder> Create(NotificationOrder order);

    /// <summary>
    /// Gets a list of notification orders where requestedSendTime has passed
    /// </summary>
    /// <returns>A list of notification orders</returns>
    public Task<List<NotificationOrder>> GetPastDueOrdersAndSetProcessingState();

    /// <summary>
    /// Sets processing status of an order
    /// </summary>
    public Task SetProcessingStatus(Guid orderId, OrderProcessingStatus status);

    /// <summary>
    /// Gets an order based on the provided id
    /// </summary>
    /// <param name="id">The order id</param>
    /// <returns>A notification order if it exists</returns>
    public Task<NotificationOrder> GetOrderById(Guid id);

    /// <summary>
    /// Gets an order based on the provided senders reference
    /// </summary>
    /// <param name="sendersReference">The senders reference</param>
    /// <returns>A notification order if it exists</returns>
    public Task<NotificationOrder> GetOrderBySendersReference(string sendersReference);
}