using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Service for querying notification log entries.
/// </summary>
public interface INotificationLogService
{
    /// <summary>
    /// Retrieves all notification log entries matching the given identifier.
    /// </summary>
    /// <param name="id">The identifier value to look up.</param>
    /// <param name="type">The type of identifier provided.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of matching notification log entries.</returns>
    Task<IEnumerable<NotificationLogEntry>> GetNotificationLogEntries(string id, NotificationLogIdType type, CancellationToken cancellationToken);
}
