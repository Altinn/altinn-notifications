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
        private const string _getDailySmsMetric = "SELECT * FROM notifications.get_sms_metrics($1, $2, $3);";  // day, month, year
        private const string _getDailyEmailMetric = "SELECT * FROM notifications.get_email_metrics($1, $2, $3);";  // day, month, year
        private const string _getEmailNotificationsMetricsSql = "SELECT anytimecount, daytimecount, composedcount FROM notifications.get_email_notifications_metrics()";
        private const string _getSmsNotificationsMetricsSql = "SELECT anytimecount, daytimecount FROM notifications.get_sms_notifications_metrics()";
        private const string _getOrderMetricsSql = "SELECT ordersnotduecount, orderspastduecount FROM notifications.get_order_metrics()";
        private const string _getOrderNotDueDistributionMetricsSql = "SELECT day, count FROM notifications.get_orders_notdue_distribution_metrics()";

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

        /// <inheritdoc/>
        public async Task<DailyMetrics<DailySmsMetricsRecord>> GetDailySmsMetrics(int day, int month, int year, CancellationToken cancellationToken)
        {
            DailyMetrics<DailySmsMetricsRecord> metrics = new()
            {
                Day = day,
                Month = month,
                Year = year
            };

            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getDailySmsMetric);
            pgcom.CommandTimeout = 300; // 5 minutes

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, day);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, month);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, year);
            await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    DailySmsMetricsRecord smsRow = new()
                    {
                        SmsId = reader.GetValue<long>("sms_id"),
                        ShipmentId = reader.GetValue<Guid>("shipmentid").ToString(),
                        SendersReference = reader.GetValue<string?>("senders_reference") ?? string.Empty,
                        RequestedSendTime = reader.GetValue<DateTime>("requestedsendtime").ToString("O"),
                        CreatorName = reader.GetValue<string>("creatorname"),
                        ResourceId = reader.GetValue<string?>("resourceid"),
                        Result = reader.GetValue<string>("result"),
                        GatewayReference = reader.GetValue<string?>("gatewayreference") ?? string.Empty,
                        Rate = reader.GetValue<string>("rate"),
                        MobileNumberPrefix = reader.GetValue<string>("mobilenumber_prefix"),
                        AltinnSmsCount = reader.GetValue<int?>("altinn_sms_count") ?? 0,
                        AltinnSmsBodyLength = reader.GetValue<int?>("altinn_sms_body_length") ?? 0,
                        AltinnSmsCustomBodyLength = reader.GetValue<int?>("altinn_sms_custom_body_length") ?? 0
                    };

                    metrics.Metrics.Add(smsRow);
                }
            }

            return metrics;
        }

        /// <inheritdoc/>
        public async Task<DailyMetrics<DailyEmailMetricsRecord>> GetDailyEmailMetrics(int day, int month, int year, CancellationToken cancellationToken)
        {
            DailyMetrics<DailyEmailMetricsRecord> metrics = new()
            {
                Day = day,
                Month = month,
                Year = year
            };

            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getDailyEmailMetric);
            pgcom.CommandTimeout = 300; // 5 minutes

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, day);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, month);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, year);
            await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    DailyEmailMetricsRecord emailRow = new()
                    {
                        EmailId = reader.GetValue<long>("email_id"),
                        ShipmentId = reader.GetValue<Guid>("shipmentid").ToString(),
                        SendersReference = reader.GetValue<string?>("senders_reference") ?? string.Empty,
                        RequestedSendTime = reader.GetValue<DateTime>("requestedsendtime").ToString("O"),
                        CreatorName = reader.GetValue<string>("creatorname"),
                        ResourceId = reader.GetValue<string?>("resourceid"),
                        Result = reader.GetValue<string>("result"),
                        OperationId = reader.GetValue<string?>("operationid") ?? string.Empty,
                    };

                    metrics.Metrics.Add(emailRow);
                }
            }

            return metrics;
        }

        /// <inheritdoc/>
        public async Task<(long AnytimeCount, long DaytimeCount, long ComposedCount)> GetEmailNotificationsMetrics(CancellationToken cancellationToken)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEmailNotificationsMetricsSql);
            await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (0, 0, 0);
            }

            long anytimeCount = reader.GetValue<long?>("anytimecount") ?? 0;
            long daytimeCount = reader.GetValue<long?>("daytimecount") ?? 0;
            long composedCount = reader.GetValue<long?>("composedcount") ?? 0;
            return (anytimeCount, daytimeCount, composedCount);
        }

        /// <inheritdoc/>
        public async Task<(long AnytimeCount, long DaytimeCount)> GetSmsNotificationsMetrics(CancellationToken cancellationToken)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSmsNotificationsMetricsSql);
            await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (0, 0);
            }

            long anytimeCount = reader.GetValue<long?>("anytimecount") ?? 0;
            long daytimeCount = reader.GetValue<long?>("daytimecount") ?? 0;
            return (anytimeCount, daytimeCount);
        }

        /// <inheritdoc/>
        public async Task<(long OrdersNotDueCount, long OrdersPastDueCount)> GetOrderMetrics(CancellationToken cancellationToken)
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrderMetricsSql);
            await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (0, 0);
            }

            long ordersNotDueCount = reader.GetValue<long?>("ordersnotduecount") ?? 0;
            long ordersPastDueCount = reader.GetValue<long?>("orderspastduecount") ?? 0;
            return (ordersNotDueCount, ordersPastDueCount);
        }

        /// <inheritdoc/>
        public async Task<List<(DateOnly Day, long Count)>> GetOrderNotDueDistributionMetrics(CancellationToken cancellationToken)
        {
            List<(DateOnly Day, long Count)> rows = [];
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getOrderNotDueDistributionMetricsSql);
            await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                DateOnly day = reader.GetValue<DateOnly?>("day") ?? default;
                long count = reader.GetValue<long?>("count") ?? 0;
                rows.Add((day, count));
            }

            return rows;
        }
    }
}
