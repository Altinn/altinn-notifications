using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Service for handling metrics for notifications
    /// </summary>
    public class MetricsService : IMetricsService, ISmsMetricsService
    {
        private readonly IMetricsRepository _metricsRepository;
        private const int DaysOffsetForSmsMetrics = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsService"/> class.
        /// </summary>
        public MetricsService(IMetricsRepository metricsRepository)
        {
            _metricsRepository = metricsRepository;
        }

        /// <inheritdoc/>
        public async Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year)
        {
            return await _metricsRepository.GetMonthlyMetrics(month, year);
        }

        /// <inheritdoc/>
        public async Task<DailySmsMetrics> GetDailySmsMetrics()
        {
            var date = DateTime.UtcNow.AddDays(-DaysOffsetForSmsMetrics);
            return await _metricsRepository.GetDailySmsMetrics(date.Day, date.Month, date.Year);
        }
    }
}
