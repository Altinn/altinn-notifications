using System.Net;

using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
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
    public async Task GetSmsDailyMetrics_RetunrsOk()
    {
        // Arrange
        Mock<IMetricsService> serviceMock = new();
        serviceMock
            .Setup(e => e.GetDailySmsMetrics())
            .ReturnsAsync(new DailySmsMetrics());
        serviceMock.Setup(e => e.GetParquetFile(It.IsAny<DailySmsMetrics>()))
            .ReturnsAsync(new MetricsSummary());

        var client = GetTestClient(
            metricsService: serviceMock.Object);

        string url = _basePath + "/sms";
        using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.Verify(e => e.GetDailySmsMetrics(), Times.Once);
        serviceMock.Verify(e => e.GetParquetFile(It.IsAny<DailySmsMetrics>()), Times.Once);
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
