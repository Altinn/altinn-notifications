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
    /// <param name="orderId">The ID of the notification order.</param>
    /// <returns>The number of rows inserted.</returns>
    Task<int> InsertAsync(Guid orderId);

    /// <summary>
    /// Retrieves all notification log entries for a given ID.
    /// </summary>
    /// <param name="id">The ID associated with the given type.</param>
    /// <param name="type">The type of ID used to retrieve notification log entries.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A collection of notification log entries.</returns>
    Task<IEnumerable<NotificationLogEntry>> GetNotificationLogEntries(string id, NotificationLogIdType type, CancellationToken cancellationToken);
}
