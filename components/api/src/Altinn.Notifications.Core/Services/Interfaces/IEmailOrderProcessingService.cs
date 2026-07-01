using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service specific to email orders.
/// </summary>
public interface IEmailOrderProcessingService
{
    /// <summary>
    /// Processes a notification order, performing contact point lookup, then building
    /// in-memory email notifications without persisting them.
    /// </summary>
    Task<EmailOrderProcessingResult> ProcessOrder(NotificationOrder order);

    /// <summary>
    /// Processes a notification order for the provided list of recipients without
    /// looking up additional recipient data. Returns in-memory notifications
    /// without persisting them.
    /// </summary>
    Task<EmailOrderProcessingResult> ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients);

    /// <summary>
    /// Retries processing of an order, performing contact point lookup, then building
    /// in-memory email notifications without persisting them.
    /// </summary>
    Task<EmailOrderProcessingResult> ProcessOrderRetry(NotificationOrder order);

    /// <summary>
    /// Retries processing of a notification order for the provided list of recipients
    /// without looking up additional recipient data. Returns in-memory notifications
    /// without persisting them.
    /// </summary>
    Task<EmailOrderProcessingResult> ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients);
}
