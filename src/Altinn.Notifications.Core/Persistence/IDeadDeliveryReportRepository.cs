using Altinn.Notifications.Core.Enums;
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
    Task<DeadDeliveryReport> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all dead delivery reports from the data source.
    /// </summary>
    /// <param name="fromDate">Start date for the filtered response</param>
    /// <param name="reason">Filter based on reason code</param>
    /// <param name="channel">Type of delivery report email/sms</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see
    /// cref="DeadDeliveryReport"/> objects representing all dead delivery reports. If no reports are found, the list
    /// will be empty.</returns>
    Task<List<DeadDeliveryReport>> GetAllAsync(DateTime fromDate, string reason, DeliveryReportChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a dead delivery report to the repository.
    /// </summary>
    /// <param name="report">The dead delivery report to save.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation containing the ID of the inserted row.</returns>
    Task<long> InsertAsync(DeadDeliveryReport report, CancellationToken cancellationToken = default);
}
