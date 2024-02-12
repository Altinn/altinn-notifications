using Altinn.Notifications.Core.Models.Metrics;

namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Interface for repository operations related to notification metrics
    /// </summary>
    public interface INotificationMetricsRepository
    {
        /// <summary>
        /// Retrieved the monthly notification metrics for a given month and year
        /// </summary>
        public Task<MonthlyNotificationMetrics> GetMontlyNotificationMetrics(int month, int year);
    }
}
