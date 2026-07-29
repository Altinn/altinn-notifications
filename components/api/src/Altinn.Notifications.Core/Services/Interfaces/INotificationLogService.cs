using Altinn.Notifications.Core.Models.NotificationLog;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides access to notification log entries.
/// </summary>
public interface INotificationLogService
{
    /// <summary>
    /// Gets notification log entries for a dialog.
    /// </summary>
    /// <param name="dialogId">The Dialogporten dialog identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Notification log entries associated with the dialog.</returns>
    Task<IReadOnlyList<NotificationLogEntry>> GetByDialogId(string? dialogId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets notification log entries for a shipment.
    /// </summary>
    /// <param name="shipmentId">The shipment identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Notification log entries associated with the shipment.</returns>
    Task<IReadOnlyList<NotificationLogEntry>> GetByShipmentId(Guid shipmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets notification log entries for a transmission.
    /// </summary>
    /// <param name="transmissionId">The Dialogporten transmission identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Notification log entries associated with the transmission.</returns>
    Task<IReadOnlyList<NotificationLogEntry>> GetByTransmissionId(string transmissionId, CancellationToken cancellationToken);
}
