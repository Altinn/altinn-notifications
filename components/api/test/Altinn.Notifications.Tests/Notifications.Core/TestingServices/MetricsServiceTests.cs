using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Parquet;
using Parquet.Meta;
using Parquet.Serialization;
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
            _metricsRepositoryMock
                .Setup(r => r.GetOrderNotDueDistributionMetrics(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

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

            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

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

            var service = new MetricsService(_metricsRepositoryMock.Object, _backlogMetricsMock.Object, _loggerMock.Object, _hostEnvironmentMock.Object);

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

        [Fact]
        public void GenerateOrderDistributionTable_FormatsDateAndRangeLabels()
        {
            // Arrange
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            var input = new List<(DateOnly Day, long Count)>
            {
                (today, 1),
                (today.AddDays(1), 2),
                (today.AddDays(8), 3),
                (today.AddDays(40), 4),
                (today.AddDays(90), 5),
            };

            // Act
            List<(string IntervalLabel, long Count)> result = InvokeGenerateOrderDistributionTable(input);

            // Assert
            Assert.Contains(result, r => r.IntervalLabel == today.ToString("yyyy-MM-dd") && r.Count == 1);
            Assert.Contains(result, r => r.IntervalLabel == today.AddDays(1).ToString("yyyy-MM-dd") && r.Count == 2);
            Assert.Contains(result, r => r.IntervalLabel == $"{today.AddDays(8):yyyy-MM-dd} - {today.AddDays(37):yyyy-MM-dd}" && r.Count == 3);
            Assert.Contains(result, r => r.IntervalLabel == $"{today.AddDays(38):yyyy-MM-dd} - {today.AddDays(67):yyyy-MM-dd}" && r.Count == 4);
            Assert.Contains(result, r => r.IntervalLabel == $"{today.AddDays(68):yyyy-MM-dd} - {today.AddDays(167):yyyy-MM-dd}" && r.Count == 5);
        }

        [Fact]
        public void GenerateOrderDistributionTable_StopsWhenRemainingIntervalsAreZero()
        {
            // Arrange
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            var input = new List<(DateOnly Day, long Count)>
            {
                (today.AddDays(8), 10),
                (today.AddDays(90), 0),
            };

            // Act
            List<(string IntervalLabel, long Count)> result = InvokeGenerateOrderDistributionTable(input);

            // Assert
            Assert.Contains(result, r => r.IntervalLabel == $"{today.AddDays(8):yyyy-MM-dd} - {today.AddDays(37):yyyy-MM-dd}" && r.Count == 10);
            Assert.DoesNotContain(result, r => r.IntervalLabel == $"{today.AddDays(38):yyyy-MM-dd} - {today.AddDays(67):yyyy-MM-dd}");
        }

        [Fact]
        public void GenerateOrderDistributionTable_AllZeroRows_ReturnsEmptyTable()
        {
            // Arrange
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            var input = new List<(DateOnly Day, long Count)>
            {
                (today, 0),
                (today.AddDays(1), 0),
                (today.AddDays(50), 0),
            };

            // Act
            List<(string IntervalLabel, long Count)> result = InvokeGenerateOrderDistributionTable(input);

            // Assert
            Assert.Empty(result);
        }

        private static List<(string IntervalLabel, long Count)> InvokeGenerateOrderDistributionTable(List<(DateOnly Day, long Count)> orderDistribution)
        {
            MethodInfo method = typeof(MetricsService).GetMethod("GenerateOrderDistributionTable", BindingFlags.NonPublic | BindingFlags.Static)!;
            object? rawResult = method.Invoke(null, [orderDistribution]);
            Assert.NotNull(rawResult);
            return Assert.IsType<List<(string IntervalLabel, long Count)>>(rawResult);
        }
    }
}
