using System.Collections.Immutable;

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
    public Task<IImmutableList<NotificationLogEntry>> GetByDialogId(string dialogId, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetByDialogId(dialogId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IImmutableList<NotificationLogEntry>> GetByTransmissionId(string transmissionId, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetByTransmissionId(transmissionId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IImmutableList<NotificationLogEntry>> GetByDialogAndTransmission(string dialogId, string transmissionId, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetByDialogAndTransmission(dialogId, transmissionId, cancellationToken);
    }
}
