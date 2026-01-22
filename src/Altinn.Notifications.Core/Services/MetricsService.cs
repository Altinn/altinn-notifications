using System.Security.Cryptography;
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
        private readonly ILogger<MetricsService> _logger;
        private readonly IHostEnvironment _hostEnvironment;
        private const int DaysOffsetForSmsMetrics = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsService"/> class.
        /// </summary>
        public MetricsService(IMetricsRepository metricsRepository, ILogger<MetricsService> logger, IHostEnvironment hostEnvironment)
        {
            _metricsRepository = metricsRepository;
            _logger = logger;
            _hostEnvironment = hostEnvironment;
        }

        /// <inheritdoc/>
        public async Task<MonthlyNotificationMetrics> GetMonthlyMetrics(int month, int year)
        {
            return await _metricsRepository.GetMonthlyMetrics(month, year);
        }

        /// <inheritdoc/>
        public async Task<DailySmsMetrics> GetDailySmsMetrics()
        {
            var date = DateTime.UtcNow.AddDays(-DaysOffsetForSmsMetrics);
            return await _metricsRepository.GetDailySmsMetrics(date.Day, date.Month, date.Year);
        }

        /// <inheritdoc/>
        public async Task<MetricsSummary> GetParquetFile(DailySmsMetrics metrics)
        {
            var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(metrics);

            var env = string.IsNullOrEmpty(_hostEnvironment.EnvironmentName) ? "Unknown" : _hostEnvironment.EnvironmentName;
            var fileName = $"{metrics.Year}{metrics.Month:00}{metrics.Day:00}_sms_notifications_{env}.parquet";

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

        private async Task<(Stream ParquetStream, string FileHash, long FileSize)> GenerateParquetFileStream(DailySmsMetrics metrics)
        {
            _logger.LogInformation("Generating daily summary parquet file.");

            var parquetData = metrics.Metrics;

            var memoryStream = new MemoryStream();

            await ParquetSerializer.SerializeAsync(parquetData, memoryStream);
            memoryStream.Position = 0;

            var hash = Convert.ToBase64String(await MD5.HashDataAsync(memoryStream));
            memoryStream.Position = 0;

            _logger.LogInformation("Successfully generated daily summary parquet file stream");

            return (memoryStream, hash, memoryStream.Length);
        }
    }
}
