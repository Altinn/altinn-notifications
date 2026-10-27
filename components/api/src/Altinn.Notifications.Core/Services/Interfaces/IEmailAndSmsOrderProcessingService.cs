using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines operations for processing notification orders that involve both email and SMS delivery channels.
/// </summary>
public interface IEmailAndSmsOrderProcessingService
{
    /// <summary>
    /// Processes a notification order through the email and SMS delivery channels.
    /// Returns an in-memory result containing all materialized notifications; does not persist.
    /// </summary>
    /// <param name="order">The notification order containing recipients, content templates, and delivery preferences.</param>
    /// <returns>The materialized <see cref="OrderProcessingResult"/>, not yet persisted.</returns>
    Task<OrderProcessingResult> ProcessOrderAsync(NotificationOrder order);

    /// <summary>
    /// Retries processing a previously failed notification order.
    /// Returns an in-memory result containing all materialized notifications; does not persist.
    /// </summary>
    /// <param name="order">The notification order to retry processing.</param>
    /// <returns>The materialized <see cref="OrderProcessingResult"/>, not yet persisted.</returns>
    Task<OrderProcessingResult> ProcessOrderRetryAsync(NotificationOrder order);
}
