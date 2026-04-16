using Altinn.Notifications.Core.Models.Metrics;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface for handling metrics for notifications
    /// </summary>
    public interface IMetricsService
    {
        /// <summary>
        /// Retrieves the monthly metrics for the provided month and year
        /// </summary>
        public Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year);

        /// <summary>
        /// Retrieves the daily metrics 
        /// </summary>
        public Task<DailyMetrics<DailySmsMetricsRecord>> GetDailySmsMetrics(CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the daily metrics 
        /// </summary>
        public Task<DailyMetrics<DailyEmailMetricsRecord>> GetDailyEmailMetrics(CancellationToken cancellationToken);

        /// <summary>
        /// Create a Parquet file stream from the provided daily SMS metrics
        /// </summary>
        Task<MetricsSummary> GetParquetFile<T>(DailyMetrics<T> metrics, CancellationToken cancellationToken);
    }
}
