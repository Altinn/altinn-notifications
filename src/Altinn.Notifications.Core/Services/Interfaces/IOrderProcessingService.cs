namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service
/// </summary>
public interface IOrderProcessingService
{
    /// <summary>
    /// Processes a batch of past due orders
    /// </summary>
    public Task ProcessPastDueOrders();

    /// <summary>
    /// Processes a batch of pending orders
    /// </summary>
    public Task ProcessPendingOrders();
}