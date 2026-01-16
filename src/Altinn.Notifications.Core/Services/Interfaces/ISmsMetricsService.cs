using Altinn.Notifications.Core.Models.Metrics;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface for handling metrics for notifications
    /// </summary>
    public interface ISmsMetricsService
    {
        /// <summary>
        /// Retrieves the daily metrics for the day specified
        /// </summary>
        public Task<DailySmsMetrics> GetDailySmsMetrics();
    }
}
