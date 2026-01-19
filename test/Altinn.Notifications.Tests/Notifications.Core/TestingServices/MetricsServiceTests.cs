using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
        private readonly Mock<ILogger<MetricsService>> _loggerMock;
        private readonly Mock<IHostEnvironment> _hostEnvironmentMock;

        public MetricsServiceTests()
        {
            _metricsRepositoryMock = new Mock<IMetricsRepository>();
            _loggerMock = new Mock<ILogger<MetricsService>>();
            _hostEnvironmentMock = new Mock<IHostEnvironment>();
            _hostEnvironmentMock.SetupGet(h => h.EnvironmentName).Returns("UnitTest");
        }

        [Fact]
        public async Task GetDailySmsMetrics_ReturnsValueFromRepository()
        {
            // Arrange
            var expected = new DailySmsMetrics
            {
                Year = DateTime.UtcNow.Year,
                Month = DateTime.UtcNow.Month,
                Day = DateTime.UtcNow.Day,
                Metrics = new List<SmsRow>()
            };

            _metricsRepositoryMock
                .Setup(r => r.GetDailySmsMetrics(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(expected);

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            DailySmsMetrics actual = await service.GetDailySmsMetrics();

            // Assert
            Assert.Same(expected, actual);
            _metricsRepositoryMock.Verify(r => r.GetDailySmsMetrics(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task GetParquetFile_ReturnsMetricsSummary_WithStreamHashAndSizeAndEnvironment()
        {
            // Arrange
            var metrics = new DailySmsMetrics
            {
                Year = 2026,
                Month = 1,
                Day = 15,
                Metrics = new List<SmsRow>() // empty list is fine for serialization
            };

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            MetricsSummary summary = await service.GetParquetFile(metrics);

            // Assert - basic invariants
            Assert.NotNull(summary);
            Assert.NotNull(summary.FileStream);
            Assert.False(string.IsNullOrWhiteSpace(summary.FileName));
            Assert.Equal("UnitTest", summary.Environment);
            Assert.Equal(metrics.Metrics.Count, summary.TotalFileTransferCount);

            // Read the returned stream to compute expected hash/size
            await using (summary.FileStream)
            {
                using var ms = new MemoryStream();
                await summary.FileStream.CopyToAsync(ms);
                byte[] bytes = ms.ToArray();

                string expectedHash = Convert.ToBase64String(MD5.HashData(bytes));
                long expectedSize = bytes.Length;

                Assert.Equal(expectedSize, summary.FileSizeBytes);
                Assert.Equal(expectedHash, summary.FileHash);
            }
        }
    }
}
