namespace Altinn.Notifications.Tools.DlqManager.Services.Queues;

/// <summary>
/// Provides DLQ management operations for <c>altinn.notifications.orders.pastdue</c>.
/// </summary>
public interface IPastDueOrdersQueueService
{
    Task RunMenuAsync();
}
