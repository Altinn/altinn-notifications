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
    }
}
