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
        /// Retrieves the daily SMS metrics for the previous day.
        /// </summary>
        public Task<DailyMetrics<DailySmsMetricsRecord>> GetDailySmsMetrics(CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the daily email metrics for the previous day.
        /// </summary>
        public Task<DailyMetrics<DailyEmailMetricsRecord>> GetDailyEmailMetrics(CancellationToken cancellationToken);

        /// <summary>
        /// Creates a Parquet file stream from the provided daily metrics (SMS or email).
        /// </summary>
        Task<MetricsSummary> GetParquetFile<T>(DailyMetrics<T> metrics, CancellationToken cancellationToken);
    }
}
