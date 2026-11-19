using System.Security.Cryptography;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Parquet.Serialization;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Service for handling metrics for notifications
    /// </summary>
    public class MetricsService : IMetricsService
    {
        private readonly IMetricsRepository _metricsRepository;
        private readonly IBacklogMetrics _backlogMetrics;
        private readonly ILogger<MetricsService> _logger;
        private readonly IHostEnvironment _hostEnvironment;
        private const int DaysOffsetForDailyMetrics = 1; // Fetch yesterdays metrics

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsService"/> class.
        /// </summary>
        public MetricsService(
            IMetricsRepository metricsRepository,
            IBacklogMetrics backlogMetrics,
            ILogger<MetricsService> logger,
            IHostEnvironment hostEnvironment)
        {
            _metricsRepository = metricsRepository;
            _backlogMetrics = backlogMetrics;
            _logger = logger;
            _hostEnvironment = hostEnvironment;
        }

        /// <inheritdoc/>
        public async Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year)
        {
            return await _metricsRepository.GetMonthlyMetrics(month, year);
        }

        /// <inheritdoc/>
        public async Task<DailyMetrics<DailySmsMetricsRecord>> GetDailySmsMetrics(CancellationToken cancellationToken)
        {
            var date = DateTime.UtcNow.AddDays(-DaysOffsetForDailyMetrics);
            return await _metricsRepository.GetDailySmsMetrics(date.Day, date.Month, date.Year, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<DailyMetrics<DailyEmailMetricsRecord>> GetDailyEmailMetrics(CancellationToken cancellationToken)
        {
            var date = DateTime.UtcNow.AddDays(-DaysOffsetForDailyMetrics);
            return await _metricsRepository.GetDailyEmailMetrics(date.Day, date.Month, date.Year, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<MetricsSummary> GetParquetFile<T>(DailyMetrics<T> metrics, CancellationToken cancellationToken)
        {
            var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(metrics, cancellationToken);

            var env = string.IsNullOrEmpty(_hostEnvironment.EnvironmentName) ? "Unknown" : _hostEnvironment.EnvironmentName;
            var type = typeof(T).Name switch
            {
                nameof(DailySmsMetricsRecord) => "sms",
                nameof(DailyEmailMetricsRecord) => "email",
                _ => throw new InvalidOperationException($"Unsupported metrics type: {typeof(T).Name}")
            };

            var fileName = $"{metrics.Year}{metrics.Month:00}{metrics.Day:00}_{type}_notifications_{env}.parquet";

            var response = new MetricsSummary
            {
                FileStream = parquetStream,
                FileName = fileName,
                FileHash = fileHash,
                FileSizeBytes = fileSize,
                TotalFileTransferCount = metrics.Metrics.Count,
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = env,
            };
            return response;
        }

        /// <inheritdoc/>
        public async Task TriggerGetMetrics(CancellationToken cancellationToken)
        {
            var (emailNotificationsAnytime, emailNotificationsDaytime, emailNotificationsComposed) = await _metricsRepository.GetEmailNotificationsMetrics(cancellationToken);
            var (smsNotificationsAnytime, smsNotificationsDaytime) = await _metricsRepository.GetSmsNotificationsMetrics(cancellationToken);
            var (ordersNotDue, ordersPastDue, ordersFailed) = await _metricsRepository.GetOrderMetrics(cancellationToken);

            _backlogMetrics.RecordBacklogMetrics(
                emailNotificationsAnytime,
                emailNotificationsDaytime,
                emailNotificationsComposed,
                smsNotificationsAnytime,
                smsNotificationsDaytime,
                ordersNotDue,
                ordersPastDue,
                ordersFailed);

            await TraceOrderDistribution(cancellationToken);
        }

        private async Task TraceOrderDistribution(CancellationToken cancellationToken)
        {
            var orderDistribution = await _metricsRepository.GetOrderNotDueDistributionMetrics(cancellationToken);
            var distributionTable = GenerateOrderDistributionTable(orderDistribution);

            DateTime timestamp = DateTime.Now;
            int i = 0;
            foreach (var row in distributionTable)
            {
                // Warning level is used to avoid global filtering
                // The log message will be picked up by a Grafana table
                // Add colon to interval label to avoid azure monitor formatting. Will be removed in Grafana table.
                if (row.Count > 0)
                {
                    _logger.LogWarning("Orders not due distribution for {Timestamp} {IntervalLabel}: {Count} {Index}", timestamp, $":{row.IntervalLabel}", row.Count, i++);
                }
            }
        }

        private static List<(string IntervalLabel, long Count)> GenerateOrderDistributionTable(List<(DateOnly Day, long Count)> orderDistribution)
        {
            var table = new List<(string IntervalLabel, long Count)>();
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            var (countsByOffset, _) = BuildOrderDistributionOffsets(orderDistribution, today);
            var maxRelevantOffset = GetMaxRelevantOffset(countsByOffset);
            if (maxRelevantOffset < 0)
            {
                return table;
            }

            AddTodayRows(table, countsByOffset, maxRelevantOffset, today);
            AddFutureRows(table, countsByOffset, maxRelevantOffset, today);

            return table;
        }

        private static (Dictionary<int, long> CountsByOffset, int MaxOffset) BuildOrderDistributionOffsets(
            List<(DateOnly Day, long Count)> orderDistribution,
            DateOnly today)
        {
            var countsByOffset = new Dictionary<int, long>();
            var maxOffset = -1;

            foreach (var row in orderDistribution)
            {
                var offset = row.Day.DayNumber - today.DayNumber;
                if (offset < 0)
                {
                    continue;
                }

                countsByOffset[offset] = countsByOffset.GetValueOrDefault(offset) + row.Count;
                if (offset > maxOffset)
                {
                    maxOffset = offset;
                }
            }

            return (countsByOffset, maxOffset);
        }

        private static int GetMaxRelevantOffset(Dictionary<int, long> countsByOffset)
        {
            var maxRelevantOffset = -1;
            foreach (var row in countsByOffset)
            {
                if (row.Value != 0 && row.Key > maxRelevantOffset)
                {
                    maxRelevantOffset = row.Key;
                }
            }

            return maxRelevantOffset;
        }

        private static void AddTodayRows(List<(string IntervalLabel, long Count)> table, Dictionary<int, long> countsByOffset, int maxRelevantOffset, DateOnly today)
        {
            for (var i = 0; i <= 7 && i <= maxRelevantOffset; i++)
            {
                countsByOffset.TryGetValue(i, out long count);
                DateOnly day = today.AddDays(i);
                table.Add((day.ToString("yyyy-MM-dd"), count));
            }
        }

        private static void AddFutureRows(List<(string IntervalLabel, long Count)> table, Dictionary<int, long> countsByOffset, int maxOffset, DateOnly today)
        {
            var startOffset = 8;
            var next30BucketCount = 0;
            while (startOffset <= maxOffset)
            {
                var isNext30Bucket = next30BucketCount < 2;
                var bucketSize = isNext30Bucket ? 30 : 100;
                var endOffset = startOffset + bucketSize - 1;

                long bucketCount = 0;
                for (var dayOffset = startOffset; dayOffset <= endOffset; dayOffset++)
                {
                    if (countsByOffset.TryGetValue(dayOffset, out long count))
                    {
                        bucketCount += count;
                    }
                }

                DateOnly startDate = today.AddDays(startOffset);
                DateOnly endDate = today.AddDays(endOffset);
                table.Add(($"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}", bucketCount));
                startOffset = endOffset + 1;
                if (isNext30Bucket)
                {
                    next30BucketCount++;
                }
            }
        }

        private async Task<(Stream ParquetStream, string FileHash, long FileSize)> GenerateParquetFileStream<T>(DailyMetrics<T> metrics, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Generating daily summary parquet file.");

            var parquetData = metrics.Metrics;

            var memoryStream = new MemoryStream();

            await ParquetSerializer.SerializeAsync(parquetData, memoryStream, cancellationToken: cancellationToken);
            memoryStream.Position = 0;

            var hash = Convert.ToBase64String(await MD5.HashDataAsync(memoryStream, cancellationToken));
            memoryStream.Position = 0;

            _logger.LogInformation("Successfully generated daily summary parquet file stream");

            return (memoryStream, hash, memoryStream.Length);
        }
    }
}
