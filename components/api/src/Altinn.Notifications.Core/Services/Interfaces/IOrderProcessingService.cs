using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines methods for processing notification orders, including initial processing and retry handling.
/// </summary>
/// <remarks>
/// This service manages the life cycle of notification orders by evaluating sending conditions,
/// processing orders through the appropriate channels, and handling retries for failed orders.
/// </remarks>
public interface IOrderProcessingService
{
    /// <summary>
    /// Processes the next ready notification order whose requested send times have passed.
    /// </summary>
    /// <param name="processRetry">A boolean indicating whether to process retry orders. If true, retrieves orders that are being retried; otherwise, retrieves orders past their send time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// <para>
    /// This method retrieves orders that are due for processing and updates their status to 'Processing'
    /// </para>
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether any orders were processed.</returns>
    public Task<bool> TryProcessOrder(bool processRetry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a notification order
    /// </summary>
    /// <param name="order">The notification order to process.</param>
    /// <param name="unitOfWork">The unit of work for database operations.</param>
    /// <remarks>
    /// <para>
    /// This method evaluates any configured sending conditions. If the conditions are met,
    /// the order is routed to the appropriate service based on its notification channel 
    /// (Email, SMS, EmailAndSms, or preferred channel).
    /// </para>
    /// <para>
    /// If the sending condition is not met, the order is marked accordingly and will not be processed.
    /// </para>
    /// </remarks>
    public Task ProcessOrder(NotificationOrder order, UnitOfWork unitOfWork);
}
