using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Parquet;
using Parquet.Meta;
using Parquet.Serialization;
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

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

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

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            DailyMetrics<DailyEmailMetricsRecord> actual = await service.GetDailyEmailMetrics(TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(expected, actual);
            _metricsRepositoryMock.Verify(r => r.GetDailyEmailMetrics(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
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

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

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

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

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
        public async Task GetParquetFile_ForEmail_WritesShipmentIdsAsUtf8AndTimestampsAsMillis()
        {
            // Arrange
            var requestedSendTime = new DateTime(2026, 1, 15, 10, 30, 45, 123, DateTimeKind.Utc);
            var resultTime = new DateTime(2026, 1, 15, 10, 31, 0, 456, DateTimeKind.Utc);
            var emailShipmentId = Guid.NewGuid().ToString();
            var orderShipmentId = Guid.NewGuid().ToString();

            var metrics = new DailyMetrics<DailyEmailMetricsRecord>
            {
                Year = 2026,
                Month = 1,
                Day = 15,
                Metrics = new List<DailyEmailMetricsRecord>
                {
                    new()
                    {
                        EmailId = 1,
                        EmailShipmentId = emailShipmentId,
                        OrderShipmentId = orderShipmentId,
                        RequestedSendTime = requestedSendTime,
                        ResultTime = resultTime,
                        CreatorName = "ttd",
                        Result = "Delivered"
                    }
                }
            };

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            MetricsSummary summary = await service.GetParquetFile(metrics, TestContext.Current.CancellationToken);

            // Assert - physical/converted types are the ones the data warehouse (ADF) accepts
            await using (summary.FileStream)
            {
                await using ParquetReader reader = await ParquetReader.CreateAsync(summary.FileStream, cancellationToken: TestContext.Current.CancellationToken);
                Dictionary<string, SchemaElement> columns = reader.Metadata!.Schema.ToDictionary(e => e.Name);

                foreach (string name in new[] { nameof(DailyEmailMetricsRecord.EmailShipmentId), nameof(DailyEmailMetricsRecord.OrderShipmentId) })
                {
                    Assert.Equal(Parquet.Meta.Type.BYTE_ARRAY, columns[name].Type);
                    Assert.Equal(ConvertedType.UTF8, columns[name].ConvertedType);
                }

                foreach (string name in new[] { nameof(DailyEmailMetricsRecord.RequestedSendTime), nameof(DailyEmailMetricsRecord.ResultTime) })
                {
                    Assert.Equal(Parquet.Meta.Type.INT64, columns[name].Type);
                    Assert.Equal(ConvertedType.TIMESTAMP_MILLIS, columns[name].ConvertedType);
                }

                summary.FileStream.Position = 0;
                IList<DailyEmailMetricsRecord> rows = (await ParquetSerializer.DeserializeAsync<DailyEmailMetricsRecord>(summary.FileStream, cancellationToken: TestContext.Current.CancellationToken)).Data;
                DailyEmailMetricsRecord row = Assert.Single(rows);
                Assert.Equal(emailShipmentId, row.EmailShipmentId);
                Assert.Equal(orderShipmentId, row.OrderShipmentId);
                Assert.Equal(requestedSendTime, row.RequestedSendTime);
                Assert.Equal(resultTime, row.ResultTime);
            }
        }

        [Fact]
        public async Task GetParquetFile_ForSms_WritesShipmentIdsAsUtf8AndTimestampsAsMillis()
        {
            // Arrange
            var requestedSendTime = new DateTime(2026, 1, 15, 10, 30, 45, 123, DateTimeKind.Utc);
            var resultTime = new DateTime(2026, 1, 15, 10, 31, 0, 456, DateTimeKind.Utc);
            var smsShipmentId = Guid.NewGuid().ToString();
            var orderShipmentId = Guid.NewGuid().ToString();

            var metrics = new DailyMetrics<DailySmsMetricsRecord>
            {
                Year = 2026,
                Month = 1,
                Day = 15,
                Metrics = new List<DailySmsMetricsRecord>
                {
                    new()
                    {
                        SmsId = 1,
                        SmsShipmentId = smsShipmentId,
                        OrderShipmentId = orderShipmentId,
                        RequestedSendTime = requestedSendTime,
                        ResultTime = resultTime,
                        CreatorName = "ttd",
                        Result = "Delivered"
                    }
                }
            };

            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

            // Act
            MetricsSummary summary = await service.GetParquetFile(metrics, TestContext.Current.CancellationToken);

            // Assert - physical/converted types are the ones the data warehouse (ADF) accepts
            await using (summary.FileStream)
            {
                await using ParquetReader reader = await ParquetReader.CreateAsync(summary.FileStream, cancellationToken: TestContext.Current.CancellationToken);
                Dictionary<string, SchemaElement> columns = reader.Metadata!.Schema.ToDictionary(e => e.Name);

                foreach (string name in new[] { nameof(DailySmsMetricsRecord.SmsShipmentId), nameof(DailySmsMetricsRecord.OrderShipmentId) })
                {
                    Assert.Equal(Parquet.Meta.Type.BYTE_ARRAY, columns[name].Type);
                    Assert.Equal(ConvertedType.UTF8, columns[name].ConvertedType);
                }

                foreach (string name in new[] { nameof(DailySmsMetricsRecord.RequestedSendTime), nameof(DailySmsMetricsRecord.ResultTime) })
                {
                    Assert.Equal(Parquet.Meta.Type.INT64, columns[name].Type);
                    Assert.Equal(ConvertedType.TIMESTAMP_MILLIS, columns[name].ConvertedType);
                }

                summary.FileStream.Position = 0;
                IList<DailySmsMetricsRecord> rows = (await ParquetSerializer.DeserializeAsync<DailySmsMetricsRecord>(summary.FileStream, cancellationToken: TestContext.Current.CancellationToken)).Data;
                DailySmsMetricsRecord row = Assert.Single(rows);
                Assert.Equal(smsShipmentId, row.SmsShipmentId);
                Assert.Equal(orderShipmentId, row.OrderShipmentId);
                Assert.Equal(requestedSendTime, row.RequestedSendTime);
                Assert.Equal(resultTime, row.ResultTime);
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
            var service = new MetricsService(_metricsRepositoryMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

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
