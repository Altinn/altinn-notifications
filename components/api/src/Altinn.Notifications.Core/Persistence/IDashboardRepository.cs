using Altinn.Notifications.Core.Models.Dashboard;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface for repository operations related to the dashboard
/// </summary>
public interface IDashboardRepository
{
    /// <summary>
    /// Retrieves all notifications (email and SMS) for a recipient identified by their national identity number within a given date range.
    /// If no date range is provided, defaults to the last 7 days.
    /// </summary>
    /// <param name="recipientNin">The national identity number of the recipient.</param>
    /// <param name="dateTimeFrom">Start of the date range (inclusive). Defaults to 7 days ago if null.</param>
    /// <param name="dateTimeTo">End of the date range (exclusive). Defaults to now if null.</param>
    /// <returns>A list of <see cref="DashboardNotification"/> matching the search criteria.</returns>
    Task<List<DashboardNotification>> GetDashboardNotificationsByNinAsync(string recipientNin, DateTime? dateTimeFrom, DateTime? dateTimeTo);
}
