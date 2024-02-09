using Altinn.Notifications.Core.Models.Metrics;

namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Interface for repository operations related to notification metrics
    /// </summary>
    public interface INotificationMetricsRepository
    {
        /// <summary>
        /// Retrieved the monthly notification metrics for a given year and month
        /// </summary>
        /// <returns></returns>
        public Task<MonthlyNotificationMetrics> GetMontlyNotificationMetrics(int year, int month);
    }
}
