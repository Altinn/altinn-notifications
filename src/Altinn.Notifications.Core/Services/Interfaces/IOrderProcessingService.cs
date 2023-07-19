using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service
/// </summary>
public interface IOrderProcessingService
{
    /// <summary>
    /// Processes a batch of past due orders
    /// </summary>
    public Task StartProcessingPastDueOrders();

    /// <summary>
    /// Processes a notification order
    /// </summary>
    public Task ProcessOrder(NotificationOrder order);
}