using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Service layer for handling dead delivery reports
/// </summary>
public interface IDeadDeliveryReportService
{
    /// <summary>
    /// Inserts a dead delivery report to the repository    
    /// </summary>
    /// <param name="report">The dead delivery report to insert</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation</param>
    /// <returns>An asynchronous Task containing the ID of the inserted row</returns>
    Task<long> InsertAsync(DeadDeliveryReport report, CancellationToken cancellationToken = default);
}
