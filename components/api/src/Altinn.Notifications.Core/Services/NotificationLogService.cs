using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Define service to handle notification log.
/// </summary>
public sealed class NotificationLogService(INotificationLogRepository notificationLogRepository) : INotificationLogService
{
    private readonly INotificationLogRepository _notificationLogRepository = notificationLogRepository;

    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationLogEntry>> GetByDialogOrTransmission(string transmissionId, string dialogId, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetByDialogOrTransmission(transmissionId, dialogId, cancellationToken);
    }
}
