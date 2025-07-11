using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service specific to SMS orders
/// </summary>
public interface ISmsOrderProcessingService
{
    /// <summary>
    /// Processes a notification order
    /// </summary>
    public Task ProcessOrder(NotificationOrder order);

    /// <summary>
    /// Processes a notification order for the provided list of recipients
    /// without looking up additional recipient data
    /// </summary>
    public Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients);

    /// <summary>
    /// Processes an instant notification order.
    /// </summary>
    /// <param name="order">
    /// The <see cref="NotificationOrder"/> containing all details about the notification order, including recipients and SMS template.
    /// </param>
    /// <param name="expiryDateTime">
    /// The <see cref="DateTime"/> specifying when the notification expires and should no longer be delivered.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> that can be used to cancel the processing operation before completion.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public Task ProcessInstantOrder(NotificationOrder order, DateTime expiryDateTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry processing of an order
    /// </summary>
    public Task ProcessOrderRetry(NotificationOrder order);

    /// <summary>
    /// Retry processing of a notification order for the provided list of recipients
    /// without looking up additional recipient data
    /// </summary>
    public Task ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients);
}
