using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Provides access to notification log entries.
/// </summary>
public sealed class NotificationLogService(INotificationLogRepository notificationLogRepository) : INotificationLogService
{
    private readonly INotificationLogRepository _notificationLogRepository = notificationLogRepository;

    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationLogEntry>> GetByDialogId(string? dialogId, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetByDialogId(dialogId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationLogEntry>> GetByShipmentId(Guid shipmentId, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetByShipmentId(shipmentId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationLogEntry>> GetByTransmissionId(string transmissionId, CancellationToken cancellationToken)
    {
        return _notificationLogRepository.GetByTransmissionId(transmissionId, cancellationToken);
    }
}
