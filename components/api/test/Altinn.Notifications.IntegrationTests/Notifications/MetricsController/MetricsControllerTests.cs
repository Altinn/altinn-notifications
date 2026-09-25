using System.Net;
using System.Text;
using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Configuration;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.MetricsController;

public class MetricsControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>
{
    private const string _basePath = "/notifications/api/v1/metrics";
    private readonly IntegrationTestWebApplicationFactory<Program> _factory;

    public MetricsControllerTests(IntegrationTestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSmsDailyMetrics_ReturnsOk()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailySmsMetricsRecord>());

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailySmsMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary
            {
                Environment = "Development",
                GeneratedAt = DateTimeOffset.UtcNow,
                FileName = "smsmetrics",
                FileStream = stream,
                FileSizeBytes = stream.Length,
                TotalFileTransferCount = 1,
                FileHash = "dummyhash"
            });

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);
        httpRequestMessage.Headers.Add("x-api-key", "valid-api-key");

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 but got {(int)response.StatusCode} with body: {responseBody}");
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("dummyhash", response.Headers.GetValues("X-File-Hash").FirstOrDefault());
        Assert.Equal("4", response.Headers.GetValues("X-File-Size").FirstOrDefault());
        Assert.Equal("1", response.Headers.GetValues("X-Total-FileTransfer-Count").FirstOrDefault());
        Assert.NotNull(response.Headers.GetValues("X-Generated-At").FirstOrDefault());
        Assert.Equal("Development", response.Headers.GetValues("X-Environment").FirstOrDefault());

        serviceMock.Verify(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()), Times.Once);
        serviceMock.Verify(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailySmsMetricsRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSmsDailyMetrics_WithoutValidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailySmsMetricsRecord>());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailySmsMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("API key required", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSmsDailyMetrics_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailySmsMetricsRecord>());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailySmsMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);
        httpRequestMessage.Headers.Add("x-api-key", "invalid-api-key");

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSmsDailyMetrics_NoConfiguredApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailySmsMetricsRecord>());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailySmsMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(metricsService: serviceMock.Object);

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEmailDailyMetrics_ReturnsOk()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailyEmailMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailyEmailMetricsRecord>());

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailyEmailMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary
            {
                Environment = "Development",
                GeneratedAt = DateTimeOffset.UtcNow,
                FileName = "emailmetrics",
                FileStream = stream,
                FileSizeBytes = stream.Length,
                TotalFileTransferCount = 1,
                FileHash = "dummyhash"
            });

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/email";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);
        httpRequestMessage.Headers.Add("x-api-key", "valid-api-key");

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("dummyhash", response.Headers.GetValues("X-File-Hash").FirstOrDefault());
        Assert.Equal("4", response.Headers.GetValues("X-File-Size").FirstOrDefault());
        Assert.Equal("1", response.Headers.GetValues("X-Total-FileTransfer-Count").FirstOrDefault());
        Assert.NotNull(response.Headers.GetValues("X-Generated-At").FirstOrDefault());
        Assert.Equal("Development", response.Headers.GetValues("X-Environment").FirstOrDefault());

        serviceMock.Verify(e => e.GetDailyEmailMetrics(It.IsAny<CancellationToken>()), Times.Once);
        serviceMock.Verify(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailyEmailMetricsRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEmailDailyMetrics_WithoutValidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailyEmailMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailyEmailMetricsRecord>());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailyEmailMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/email";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("API key required", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetEmailDailyMetrics_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailyEmailMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailyEmailMetricsRecord>());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailyEmailMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/email";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);
        httpRequestMessage.Headers.Add("x-api-key", "invalid-api-key");

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEmailDailyMetrics_NoConfiguredApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailyEmailMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyMetrics<DailyEmailMetricsRecord>());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailyMetrics<DailyEmailMetricsRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(metricsService: serviceMock.Object);

        string url = _basePath + "/email";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient GetTestClient(
        IMetricsService? metricsService = null)
    {
        metricsService ??= new Mock<IMetricsService>().Object;

        _factory.ResetInstalledMocks();
        _factory.InstallService(metricsService);
        return _factory.SharedClient;
    }
}
