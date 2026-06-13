namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository interface for inserting notification log entries.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Inserts a notification log entry.
    /// </summary>
    /// <param name="orderId">The ID of the notification order.</param>
    /// <returns>The number of rows inserted.</returns>
    Task<int> InsertAsync(Guid orderId);
}
