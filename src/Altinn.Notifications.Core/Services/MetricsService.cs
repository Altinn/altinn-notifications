using System.Diagnostics.Metrics;

using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Service for handling metrics for notifications
    /// </summary>
    public class MetricsService : IMetricsService, IDisposable
    {
        private readonly IMetricsRepository _metricsRepository;
        private readonly string _metricPrefix = "notifications";
        private static readonly Meter _meter = new(MeterName, "1.0.0");

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsService"/> class.
        /// </summary>
        public MetricsService(IMetricsRepository metricsRepository)
        {
            _metricsRepository = metricsRepository;
            TriggerPastDueOrdersCounter = _meter.CreateCounter<long>(CreateName("trigger_past_due_orders"), description: "Counter for tracking past due orders trigger events");
            TriggerDeleteOldStatusFeedRecords = _meter.CreateCounter<long>(CreateName("trigger_delete_old_status_feed_records"), description: "Counter for tracking delete old status feed records trigger events");
            TriggerSendEmailNotificationsCounter = _meter.CreateCounter<long>(CreateName("trigger_send_email_notifications"), description: "Counter for tracking send email notifications trigger events");
            TriggerTerminateExpiredNotificationsCounter = _meter.CreateCounter<long>(CreateName("trigger_terminate_expired_notifications"), description: "Counter for tracking terminate expired notifications trigger events");
            TriggerSendSmsNotificationsDaytimeCounter = _meter.CreateCounter<long>(CreateName("trigger_send_sms_notifications_daytime"), description: "Counter for tracking send SMS notifications daytime trigger events");
            TriggerSendSmsNotificationsAnytimeCounter = _meter.CreateCounter<long>(CreateName("trigger_send_sms_notifications_anytime"), description: "Counter for tracking send SMS notifications anytime trigger events");
        }

        /// <summary>
        /// Gets the name of the meter used for platform notifications.
        /// </summary>
        public static string MeterName => "platform-notifications";

        /// <inheritdoc/>
        public Counter<long> TriggerPastDueOrdersCounter { get; }

        /// <inheritdoc/>
        public Counter<long> TriggerDeleteOldStatusFeedRecords { get; }

        /// <inheritdoc/>
        public Counter<long> TriggerSendEmailNotificationsCounter { get; }

        /// <inheritdoc/>
        public Counter<long> TriggerTerminateExpiredNotificationsCounter { get; }

        /// <inheritdoc/>
        public Counter<long> TriggerSendSmsNotificationsDaytimeCounter { get; }

        /// <inheritdoc/>
        public Counter<long> TriggerSendSmsNotificationsAnytimeCounter { get; }

        /// <inheritdoc/>
        public async Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year)
        {
            return await _metricsRepository.GetMonthlyMetrics(month, year);
        }

        /// <summary>
        /// Creates a name for a metric.
        /// </summary>
        /// <param name="name">Name of the metric, separate words with dot.</param>
        /// <returns>Full metric name</returns>
        private string CreateName(string name) => $"{_metricPrefix}.{name}";

        /// <summary>
        /// Disposes the meter instance.
        /// </summary>
        public void Dispose()
        {
            _meter.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
