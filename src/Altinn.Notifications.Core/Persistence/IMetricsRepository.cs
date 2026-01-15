using Altinn.Notifications.Core.Models.Metrics;

namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Interface for repository operations related to notification metrics
    /// </summary>
    public interface IMetricsRepository
    {
        /// <summary>
        /// Retrieved the monthly notification metrics for a given month and year
        /// </summary>
        public Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year);

        /// <summary>
        /// Get the daily SMS metrics for a given day, month and year
        /// </summary>
        Task<DailySmsMetrics> GetDailySmsMetrics(int day, int month, int year);
    }
}
