using Altinn.Notifications.Core.Models.NotificationLog;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository interface for inserting notification log entries.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Inserts a notification log entry.
    /// </summary>
    /// <param name="entry">The log entry containing all notification metadata to persist.</param>
    /// <returns>The auto-generated ID of the inserted log entry.</returns>
    Task<long> InsertAsync(NotificationLogEntry entry);
}
