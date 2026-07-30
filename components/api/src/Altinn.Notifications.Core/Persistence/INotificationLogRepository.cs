using Altinn.Notifications.Core.Models.NotificationLog;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Define notification log operations.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Gets notification log entries matching the specified criteria.
    /// </summary>
    /// <param name="transmissionId">
    /// The Dialogporten transmission identifier used to filter notification log entries.
    /// </param>
    /// <param name="dialogId">
    /// The Dialogporten dialog identifier used to filter notification log entries.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A collection of notification log entries matching the specified criteria.
    /// </returns>
    Task<IReadOnlyList<NotificationLogEntry>> GetByDialogOrTransmission(string transmissionId, string dialogId, CancellationToken cancellationToken);
}
