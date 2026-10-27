using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service specific to email or SMS preferred orders.
/// </summary>
public interface IPreferredChannelProcessingService
{
    /// <summary>
    /// Processes a notification order using the preferred channel strategy.
    /// Returns an in-memory result containing all materialized notifications; does not persist.
    /// </summary>
    /// <param name="order">The notification order to process.</param>
    /// <returns>The materialized <see cref="OrderProcessingResult"/>, not yet persisted.</returns>
    Task<OrderProcessingResult> ProcessOrder(NotificationOrder order);

    /// <summary>
    /// Retries processing of a notification order using the preferred channel strategy.
    /// Returns an in-memory result containing all materialized notifications; does not persist.
    /// </summary>
    /// <param name="order">The notification order to retry processing.</param>
    /// <returns>The materialized <see cref="OrderProcessingResult"/>, not yet persisted.</returns>
    Task<OrderProcessingResult> ProcessOrderRetry(NotificationOrder order);
}
