using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Service for handling metrics for notifications
    /// </summary>
    public class MetricsService : IMetricsService
    {
        private readonly INotificationMetricsRepository _metricsRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsService"/> class.
        /// </summary>
        public MetricsService(INotificationMetricsRepository metricsRepository)
        {
            _metricsRepository = metricsRepository;
        }

        /// <inheritdoc/>
        public async Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year)
        {
            return await _metricsRepository.GetMontlyNotificationMetrics(month, year);
        }
    }
}
