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
    /// Sets processing status status on an order
    /// </summary>
    public Task SetProcessingStatus(string orderId, OrderProcessingStatus status);
}