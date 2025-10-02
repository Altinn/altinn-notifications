using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Service layer for handling dead delivery reports
/// </summary>
public interface IDeadDeliveryReportService
{
    /// <summary>
    /// Adds a dead delivery report to the repository    
    /// </summary>
    /// <param name="report">A JSON string representation of the delivery report</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation</param>
    /// <returns>An asynchronous Task containing the ID of the inserted row</returns>
    Task<long> Insert(DeadDeliveryReport report, CancellationToken cancellationToken = default);
}
