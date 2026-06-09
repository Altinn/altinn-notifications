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
    /// <returns>The auto-generated ID of the inserted log entry.</returns>
    Task<long> InsertAsync(Guid notificationId);
}
