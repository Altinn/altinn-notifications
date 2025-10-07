using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository interface for managing dead delivery reports.
/// Provides operations for persisting delivery reports that have failed repeatedly.
/// </summary>
public interface IDeadDeliveryReportRepository
{
    /// <summary>
    /// Gets a dead delivery report by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the dead delivery report</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation</param>
    /// <returns>A dead delivery report object <see cref="DeadDeliveryReport"/></returns>
    Task<DeadDeliveryReport> GetDeadDeliveryReportAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a dead delivery report to the repository.
    /// </summary>
    /// <param name="report">The dead delivery report to save.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation containing the ID of the inserted row.</returns>
    Task<long> InsertAsync(DeadDeliveryReport report, CancellationToken cancellationToken);
}
