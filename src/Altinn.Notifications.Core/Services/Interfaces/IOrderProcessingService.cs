using Altinn.Notifications.Core.Models.Orders;

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
    /// Processes a batch of notification orders whose requested send times have passed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method retrieves orders that are due for processing, updates their status to 'Processing',
    /// and publishes them to a configured Kafka topic for asynchronous handling.
    /// </para>
    /// <para>
    /// The method continues fetching batches of orders until either fewer than 50 orders are returned
    /// or the total processing time exceeds 60 seconds.
    /// </para>
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StartProcessingPastDueOrders();

    /// <summary>
    /// Processes a notification order through the appropriate channel-specific service.
    /// </summary>
    /// <param name="order">The notification order to process.</param>
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
    /// <returns>
    /// A result indicating whether the order was successfully processed or requires a retry.
    /// </returns>
    public Task<NotificationOrderProcessingResult> ProcessOrder(NotificationOrder order);

    /// <summary>
    /// Retries processing of a previously failed notification order.
    /// </summary>
    /// <param name="order">The notification order to retry processing.</param>
    /// <remarks>
    /// <para>
    /// This method re-evaluates the sending conditions and attempts to process the order again if the conditions are met.
    /// </para>
    /// <para>
    /// Unlike the initial processing, this method is more lenient. If the sending condition check fails during retry,
    /// the order is still processed.
    /// </para>
    /// <para>
    /// After retry processing, the order's status is updated based on the outcome.
    /// </para>
    /// </remarks>
    /// <returns>
    /// A result indicating whether further retry attempts are required (typically returns <c>false</c>).
    /// </returns>
    public Task ProcessOrderRetry(NotificationOrder order);
}
