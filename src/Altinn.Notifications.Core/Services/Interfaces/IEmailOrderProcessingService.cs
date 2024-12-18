using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service specific to email orders.
/// </summary>
public interface IEmailOrderProcessingService
{
    /// <summary>
    /// Processes a notification order.
    /// </summary>
    /// <param name="order">The notification order to process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessOrder(NotificationOrder order);

    /// <summary>
    /// Processes a notification order for the provided list of recipients without looking up additional recipient data.
    /// </summary>
    /// <param name="order">The notification order to process.</param>
    /// <param name="recipients">The list of recipients to process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients);

    /// <summary>
    /// Retries processing of a notification order.
    /// </summary>
    /// <param name="order">The notification order to retry processing.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessOrderRetry(NotificationOrder order);

    /// <summary>
    /// Retries processing of a notification order for the provided list of recipients without looking up additional recipient data.
    /// </summary>
    /// <param name="order">The notification order to retry processing.</param>
    /// <param name="recipients">The list of recipients to process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients);
}
