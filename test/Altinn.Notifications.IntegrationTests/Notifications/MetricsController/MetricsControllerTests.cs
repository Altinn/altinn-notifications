using System.Net;
using System.Text;
using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.MetricsController;

public class MetricsControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.MetricsController>>
{
    private const string _basePath = "/notifications/api/v1/metrics";
    private readonly IntegrationTestWebApplicationFactory<Controllers.MetricsController> _factory;

    public MetricsControllerTests(IntegrationTestWebApplicationFactory<Controllers.MetricsController> factory)
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
            .ReturnsAsync(new DailySmsMetrics());

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailySmsMetrics>(), It.IsAny<CancellationToken>()))
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
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("dummyhash", response.Headers.GetValues("X-File-Hash").FirstOrDefault());
        Assert.Equal("4", response.Headers.GetValues("X-File-Size").FirstOrDefault());
        Assert.Equal("1", response.Headers.GetValues("X-Total-FileTransfer-Count").FirstOrDefault());
        Assert.NotNull(response.Headers.GetValues("X-Generated-At").FirstOrDefault());
        Assert.Equal("Development", response.Headers.GetValues("X-Environment").FirstOrDefault());

        serviceMock.Verify(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()), Times.Once);
        serviceMock.Verify(e => e.GetParquetFile(It.IsAny<DailySmsMetrics>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSmsDailyMetrics_WithoutValidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailySmsMetrics());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailySmsMetrics>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("API key required", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetSmsDailyMetrics_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailySmsMetrics(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailySmsMetrics());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailySmsMetrics>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);
        httpRequestMessage.Headers.Add("x-api-key", "invalid-api-key");

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

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
            .ReturnsAsync(new DailySmsMetrics());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailySmsMetrics>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricsSummary());

        // Create client overriding configuration to remove the configured API key
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // override MetricsApiKey to empty (treat as not configured)
                var overrides = new Dictionary<string, string?>
                {
                    ["MetricsApiKey"] = string.Empty
                };
                config.AddInMemoryCollection(overrides!);
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(serviceMock.Object);
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient GetTestClient(
        IMetricsService? metricsService = null)
    {
        metricsService ??= new Mock<IMetricsService>().Object;

        return _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(metricsService);
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();
    }
}
