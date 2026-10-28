using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices
{
    public class MetricsServiceTests
    {
        private readonly Mock<IMetricsRepository> _metricsRepositoryMock;
        private readonly Mock<IBacklogMetrics> _backlogMetricsMock;
        private readonly Mock<ILogger<MetricsService>> _loggerMock;
        private readonly Mock<IHostEnvironment> _hostEnvironmentMock;

        public MetricsServiceTests()
        {
            _metricsRepositoryMock = new Mock<IMetricsRepository>();
            _backlogMetricsMock = new Mock<IBacklogMetrics>();
            _loggerMock = new Mock<ILogger<MetricsService>>();
            _hostEnvironmentMock = new Mock<IHostEnvironment>();
            _hostEnvironmentMock.SetupGet(h => h.EnvironmentName).Returns("UnitTest");
        }

        [Fact]
        public async Task GetDailySmsMetrics_ReturnsValueFromRepository()
        {
            // Arrange
            var expected = new DailyMetrics<DailySmsMetricsRecord>
            {
                Year = DateTime.UtcNow.Year,
                Month = DateTime.UtcNow.Month,
                Day = DateTime.UtcNow.Day,
                Metrics = new List<DailySmsMetricsRecord>()
            };

            _metricsRepositoryMock
                .Setup(r => r.GetDailySmsMetrics(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            DailyMetrics<DailySmsMetricsRecord> actual = await service.GetDailySmsMetrics(TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(expected, actual);
            _metricsRepositoryMock.Verify(r => r.GetDailySmsMetrics(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetDailyEmailMetrics_ReturnsValueFromRepository()
        {
            // Arrange
            var expected = new DailyMetrics<DailyEmailMetricsRecord>
            {
                Year = DateTime.UtcNow.Year,
                Month = DateTime.UtcNow.Month,
                Day = DateTime.UtcNow.Day,
                Metrics = new List<DailyEmailMetricsRecord>()
            };

            _metricsRepositoryMock
                .Setup(r => r.GetDailyEmailMetrics(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            DailyMetrics<DailyEmailMetricsRecord> actual = await service.GetDailyEmailMetrics(TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(expected, actual);
            _metricsRepositoryMock.Verify(r => r.GetDailyEmailMetrics(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TriggerGetMetrics_InvokesRepositoryMetricsQuery()
        {
            // Arrange
            _metricsRepositoryMock
                .Setup(r => r.GetEmailNotificationsMetrics(It.IsAny<CancellationToken>()))
                .ReturnsAsync((5, 3, 2));
            _metricsRepositoryMock
                .Setup(r => r.GetSmsNotificationsMetrics(It.IsAny<CancellationToken>()))
                .ReturnsAsync((7, 11));
            _metricsRepositoryMock
                .Setup(r => r.GetOrderMetrics(It.IsAny<CancellationToken>()))
                .ReturnsAsync((13, 17, 19));

            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            await service.TriggerGetMetrics(TestContext.Current.CancellationToken);

            // Assert
            _metricsRepositoryMock.Verify(r => r.GetEmailNotificationsMetrics(It.IsAny<CancellationToken>()), Times.Once);
            _metricsRepositoryMock.Verify(r => r.GetSmsNotificationsMetrics(It.IsAny<CancellationToken>()), Times.Once);
            _metricsRepositoryMock.Verify(r => r.GetOrderMetrics(It.IsAny<CancellationToken>()), Times.Once);
            _backlogMetricsMock.Verify(m => m.RecordBacklogMetrics(5, 3, 2, 7, 11, 13, 17, 19), Times.Once);
        }

        [Fact]
        public async Task GetParquetFile_ReturnsMetricsSummary_WithStreamHashAndSizeAndEnvironment()
        {
            // Arrange
            var metrics = new DailyMetrics<DailySmsMetricsRecord>
            {
                Year = 2026,
                Month = 1,
                Day = 15,
                Metrics = new List<DailySmsMetricsRecord>() // empty list is fine for serialization
            };

            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            MetricsSummary summary = await service.GetParquetFile(metrics, TestContext.Current.CancellationToken);

            // Assert - basic invariants
            Assert.NotNull(summary);
            Assert.NotNull(summary.FileStream);
            Assert.False(string.IsNullOrWhiteSpace(summary.FileName));
            Assert.Contains("sms", summary.FileName);
            Assert.Equal("UnitTest", summary.Environment);
            Assert.Equal(metrics.Metrics.Count, summary.TotalFileTransferCount);
            Assert.NotEqual(DateTimeOffset.MinValue, summary.GeneratedAt);

            // Read the returned stream to compute expected hash/size
            await using (summary.FileStream)
            {
                using var ms = new MemoryStream();
                await summary.FileStream.CopyToAsync(ms, TestContext.Current.CancellationToken);
                byte[] bytes = ms.ToArray();

                string expectedHash = Convert.ToBase64String(MD5.HashData(bytes));
                long expectedSize = bytes.Length;

                Assert.Equal(expectedSize, summary.FileSizeBytes);
                Assert.Equal(expectedHash, summary.FileHash);
            }
        }

        [Fact]
        public async Task GetParquetFile_ReturnsMetricsSummary_WithStreamHashAndSizeAndEnvironmentForEmail()
        {
            // Arrange
            var metrics = new DailyMetrics<DailyEmailMetricsRecord>
            {
                Year = 2026,
                Month = 1,
                Day = 15,
                Metrics = new List<DailyEmailMetricsRecord>() // empty list is fine for serialization
            };

            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            MetricsSummary summary = await service.GetParquetFile(metrics, TestContext.Current.CancellationToken);

            // Assert - basic invariants
            Assert.NotNull(summary);
            Assert.NotNull(summary.FileStream);
            Assert.False(string.IsNullOrWhiteSpace(summary.FileName));
            Assert.Contains("email", summary.FileName);
            Assert.Equal("UnitTest", summary.Environment);
            Assert.Equal(metrics.Metrics.Count, summary.TotalFileTransferCount);
            Assert.NotEqual(DateTimeOffset.MinValue, summary.GeneratedAt);

            // Read the returned stream to compute expected hash/size
            await using (summary.FileStream)
            {
                using var ms = new MemoryStream();
                await summary.FileStream.CopyToAsync(ms, TestContext.Current.CancellationToken);
                byte[] bytes = ms.ToArray();

                string expectedHash = Convert.ToBase64String(MD5.HashData(bytes));
                long expectedSize = bytes.Length;

                Assert.Equal(expectedSize, summary.FileSizeBytes);
                Assert.Equal(expectedHash, summary.FileHash);
            }
        }

        [Fact]
        public async Task GetParquetFile_ReturnsMetricsSummaryWhenNoEnv_WithEnvironmentUnknown()
        {
            // Arrange
            var metrics = new DailyMetrics<DailySmsMetricsRecord>
            {
                Year = 2026,
                Month = 1,
                Day = 28,
                Metrics = new List<DailySmsMetricsRecord>() // empty list is fine for serialization
            };

            // Use string.Empty instead of null to avoid CS8603
            _ = _hostEnvironmentMock.SetupGet(h => h.EnvironmentName).Returns(string.Empty);
            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            MetricsSummary summary = await service.GetParquetFile(metrics, TestContext.Current.CancellationToken);

            // Assert - basic invariants
            Assert.NotNull(summary);
            Assert.NotNull(summary.FileStream);
            Assert.False(string.IsNullOrWhiteSpace(summary.FileName));
            Assert.Equal("Unknown", summary.Environment);
            Assert.Equal(metrics.Metrics.Count, summary.TotalFileTransferCount);
            Assert.NotEqual(DateTimeOffset.MinValue, summary.GeneratedAt);

            // Read the returned stream to compute expected hash/size
            await using (summary.FileStream)
            {
                using var ms = new MemoryStream();
                await summary.FileStream.CopyToAsync(ms, TestContext.Current.CancellationToken);
                byte[] bytes = ms.ToArray();

                string expectedHash = Convert.ToBase64String(MD5.HashData(bytes));
                long expectedSize = bytes.Length;

                Assert.Equal(expectedSize, summary.FileSizeBytes);
                Assert.Equal(expectedHash, summary.FileHash);
            }
        }
    }
}
