using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for dashboard service operations
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Retrieves all notifications (email and SMS) for a recipient identified by their national identity number within a given date range.
    /// If no date range is provided, defaults to the last 7 days.
    /// </summary>
    /// <param name="recipientNin">The national identity number of the recipient.</param>
    /// <param name="dateTimeFrom">Start of the date range (inclusive). Defaults to 7 days ago if null.</param>
    /// <param name="dateTimeTo">End of the date range (exclusive). Defaults to now if null.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of <see cref="DashboardNotification"/> matching the search criteria, or a <see cref="ServiceError"/> if no notifications are found.</returns>
    Task<Result<List<DashboardNotification>, ServiceError>> GetNotificationsByNinAsync(string recipientNin, DateTimeOffset? dateTimeFrom, DateTimeOffset? dateTimeTo, CancellationToken cancellationToken);
}
