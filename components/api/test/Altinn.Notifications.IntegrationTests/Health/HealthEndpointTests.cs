using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Health;

public class HealthEndpointTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>
{
    private readonly IntegrationTestWebApplicationFactory<Program> _factory;

    public HealthEndpointTests(IntegrationTestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_WithHealthyDependencies_ReturnOk(string path)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Live_WithUnhealthyReadinessCheck_ReturnsOkWhileReadyReturnsServiceUnavailable()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddHealthChecks().AddCheck(
                "failing_dependency",
                () => HealthCheckResult.Unhealthy("down"),
                tags: ["ready"]);
        }));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        using HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task Ready_WithUnhealthyDependencyCheck_ReturnsOk()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddHealthChecks().AddCheck(
                "failing_soft_dependency",
                () => HealthCheckResult.Unhealthy("down"),
                tags: ["dependency"]);
        }));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}
