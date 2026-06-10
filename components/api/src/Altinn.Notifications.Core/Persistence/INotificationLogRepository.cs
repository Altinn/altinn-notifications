namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository interface for inserting notification log entries.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Inserts a notification log entry.
    /// </summary>
    /// <param name="notificationId">The ID of the notification.</param>
    /// <returns>The number of rows inserted.</returns>
    Task<int> InsertAsync(Guid notificationId);
}
