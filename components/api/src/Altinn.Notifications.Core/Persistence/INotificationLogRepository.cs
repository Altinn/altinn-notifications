using Altinn.Notifications.Core.Models.NotificationLog;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Provides access to notification log entries.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Gets notification log entries associated with the specified Dialogporten dialog.
    /// </summary>
    /// <param name="dialogId">The Dialogporten dialog identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A collection of notification log entries associated with the specified dialog.
    /// </returns>
    Task<IReadOnlyList<NotificationLogEntry>> GetByDialogId(string? dialogId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets notification log entries associated with the specified shipment.
    /// </summary>
    /// <param name="shipmentId">The shipment identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A collection of notification log entries associated with the specified shipment.
    /// </returns>
    Task<IReadOnlyList<NotificationLogEntry>> GetByShipmentId(Guid shipmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets notification log entries associated with the specified Dialogporten transmission.
    /// </summary>
    /// <param name="transmissionId">The Dialogporten transmission identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A collection of notification log entries associated with the specified transmission.
    /// </returns>
    Task<IReadOnlyList<NotificationLogEntry>> GetByTransmissionId(string transmissionId, CancellationToken cancellationToken);
}
