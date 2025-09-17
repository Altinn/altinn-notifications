using System.Diagnostics.Metrics;

using Altinn.Notifications.Core.Models.Metrics;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface for handling metrics for notifications
    /// </summary>
    public interface IMetricsService
    {
        /// <summary>
        /// Counter for tracking past due orders trigger events
        /// </summary>
        public Counter<long> TriggerPastDueOrdersCounter { get; }

        /// <summary>
        /// Counter for tracking delete old status feed records trigger events
        /// </summary>
        public Counter<long> TriggerDeleteOldStatusFeedRecords { get; }

        /// <summary>
        /// Counter for tracking send email notifications trigger events
        /// </summary>
        public Counter<long> TriggerSendEmailNotificationsCounter { get; }

        /// <summary>
        /// Counter for tracking terminate expired notifications trigger events
        /// </summary>
        public Counter<long> TriggerTerminateExpiredNotificationsCounter { get; }

        /// <summary>
        /// Counter for tracking send SMS notifications daytime trigger events
        /// </summary>
        public Counter<long> TriggerSendSmsNotificationsDaytimeCounter { get; }

        /// <summary>
        /// Counter for tracking send SMS notifications anytime trigger events
        /// </summary>
        public Counter<long> TriggerSendSmsNotificationsAnytimeCounter { get; }

        /// <summary>
        /// Retrieves the monthly metrics for the provided month and year
        /// </summary>
        public Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year);
    }
}
