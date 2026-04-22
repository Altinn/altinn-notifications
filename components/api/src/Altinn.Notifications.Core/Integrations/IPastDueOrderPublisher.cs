using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines a method for publishing past-due notification orders for processing.
/// </summary>
public interface IPastDueOrderPublisher
{
    /// <summary>
    /// Publishes a batch of past-due orders for asynchronous processing.
    /// </summary>
    /// <param name="orders">The notification orders to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes with a read-only list of <see cref="NotificationOrder"/> objects
    /// that failed to publish. An empty list means all orders were published successfully.
    /// </returns>
    Task<IReadOnlyList<NotificationOrder>> PublishAsync(
        IReadOnlyList<NotificationOrder> orders,
        CancellationToken cancellationToken = default);
}
