using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// An implementation of the <see cref="IMetricsRepository"/> for handling metrics for notifications
    /// </summary>
    public class MetricsRepository : IMetricsRepository
    {
        private readonly NpgsqlDataSource _dataSource;

        private const string _getMonthlytMetric = "SELECT * FROM notifications.get_metrics_v2($1, $2);";  // month, year

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsRepository"/> class.
        /// </summary>
        /// <param name="dataSource">The npgsql data source.</param>
        public MetricsRepository(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        /// <inheritdoc/>
        public async Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year)
        {
            MonthlyNotificationMetrics metrics = new()
            {
                Month = month,
                Year = year
            };

            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getMonthlytMetric);

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, month);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, year);
            await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    MetricsForOrg orgMetric = new()
                    {
                        Org = reader.GetValue<string>("org"),
                        OrdersCreated = reader.GetValue<int>("placed_orders"),
                        EmailNotificationsCreated = reader.GetValue<int>("sent_emails"),
                        SuccessfulEmailNotifications = reader.GetValue<int>("succeeded_emails"),
                        SmsNotificationsCreated = reader.GetValue<int>("sent_sms"),
                        SuccessfulSmsNotifications = reader.GetValue<int>("succeeded_sms")
                    };

                    metrics.Metrics.Add(orgMetric);
                }
            }

            return metrics;
        }
    }
}
