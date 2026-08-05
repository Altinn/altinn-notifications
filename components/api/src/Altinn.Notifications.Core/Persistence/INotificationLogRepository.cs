using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.NotificationLog;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Defines notification log operations.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Retrieves notification log entries matching the specified dialog identifier.
    /// </summary>
    /// <param name="dialogId">
    /// The dialog identifier used to filter notification log entries.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// An immutable collection of notification log entries matching the specified dialog identifier.
    /// </returns>
    Task<IImmutableList<NotificationLogSummary>> GetByDialogId(string dialogId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves notification log entries matching the specified transmission identifier.
    /// </summary>
    /// <param name="transmissionId">
    /// The transmission identifier used to filter notification log entries.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// An immutable collection of notification log entries matching the specified transmission identifier.
    /// </returns>
    Task<IImmutableList<NotificationLogSummary>> GetByTransmissionId(string transmissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves notification log entries matching the specified dialog and transmission identifiers.
    /// </summary>
    /// <param name="dialogId">
    /// The dialog identifier used to filter notification log entries.
    /// </param>
    /// <param name="transmissionId">
    /// The transmission identifier used to filter notification log entries.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// An immutable collection of notification log entries matching both the specified dialog and transmission identifiers.
    /// </returns>
    Task<IImmutableList<NotificationLogSummary>> GetByDialogAndTransmission(string dialogId, string transmissionId, CancellationToken cancellationToken);
}
