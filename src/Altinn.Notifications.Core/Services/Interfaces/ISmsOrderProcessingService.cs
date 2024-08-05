using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service speficic to sms orders
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
    /// Retry processing of an order
    /// </summary>
    public Task ProcessOrderRetry(NotificationOrder order);

    /// <summary>
    /// Retryprocessing of a notification order for the provided list of recipients
    /// without looking up additional recipient data
    /// </summary>
    public Task ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients);
}
