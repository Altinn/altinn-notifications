using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service for querying notification log entries.
/// </summary>
public class NotificationLogService : INotificationLogService
{
    private readonly INotificationLogRepository _notificationLogRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationLogService"/> class.
    /// </summary>
    /// <param name="notificationLogRepository">The notification log repository.</param>
    public NotificationLogService(INotificationLogRepository notificationLogRepository)
    {
        _notificationLogRepository = notificationLogRepository;
    }

    /// <inheritdoc/>
    public Task<IEnumerable<NotificationLogEntry>> GetNotificationLogEntries(string id, NotificationLogIdType type, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetNotificationLogEntries(id, type, cancellationToken);
    }
}
