using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service for dashboard operations
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _dashboardRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardService"/> class.
    /// </summary>
    /// <param name="dashboardRepository">The dashboard repository.</param>
    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    /// <inheritdoc/>
    public async Task<Result<List<DashboardNotification>, ServiceError>> GetNotificationsByNinAsync(string recipientNin, DateTime? dateTimeFrom, DateTime? dateTimeTo, CancellationToken cancellationToken)
    {
        return await _dashboardRepository.GetDashboardNotificationsByNinAsync(recipientNin, dateTimeFrom, dateTimeTo, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<List<DashboardNotification>, ServiceError>> GetNotificationsByOrgNumberAsync(string recipientOrgNumber, DateTime? dateTimeFrom, DateTime? dateTimeTo, CancellationToken cancellationToken)
    {
        return await _dashboardRepository.GetDashboardNotificationsByOrgNumberAsync(recipientOrgNumber, dateTimeFrom, dateTimeTo, cancellationToken);
    }
}
