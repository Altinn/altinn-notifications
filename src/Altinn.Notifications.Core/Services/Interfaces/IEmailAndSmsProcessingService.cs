using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines operations for processing notification orders that involve both email and SMS delivery channels.
/// </summary>
public interface IEmailAndSmsProcessingService
{
    /// <summary>
    /// Processes a notification order by preparing and dispatching it through the email and SMS delivery channels.
    /// </summary>
    /// <param name="order">The notification order containing recipients, content templates, and delivery preferences.</param>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    public Task ProcessOrder(NotificationOrder order);

    /// <summary>
    /// Attempts to reprocess a previously failed notification order.
    /// This method implements retry logic for orders that couldn't be successfully delivered in previous attempts.
    /// </summary>
    /// <param name="order">The notification order to retry processing.</param>
    /// <returns>A task representing the asynchronous retry operation.</returns>
    public Task ProcessOrderRetry(NotificationOrder order);
}
