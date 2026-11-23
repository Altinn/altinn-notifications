using System.Net;

using Altinn.Notifications.Shared.Health;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Health;

public class HealthEndpointTests : IClassFixture<IntegrationTestWebApplicationFactory<Program>>
{
    private readonly IntegrationTestWebApplicationFactory<Program> _factory;

    public HealthEndpointTests(IntegrationTestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ready_WithUnhealthyDependencyCheck_ReturnsOk()
    {
        using var factory = CreateFactoryWithUnhealthyCheck(HealthCheckTags.Dependency);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public void MetricsPublisher_ExcludesLifecycleChecksButIncludesOthers()
    {
        var options = _factory.Services.GetRequiredService<IOptions<HealthCheckPublisherOptions>>().Value;
        var registrations = _factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        var published = registrations.Where(r => options.Predicate!(r)).Select(r => r.Name).ToList();

        Assert.Contains("notifications_health_check", published);
        Assert.DoesNotContain("notifications_startup_health_check", published);
        Assert.DoesNotContain("notifications_shutdown_health_check", published);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/startup")]
    public async Task HealthEndpoints_WithHealthyDependencies_ReturnOk(string path)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_WhenShutdownHasStarted_ReturnsServiceUnavailableWhileLiveStaysOk()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(l => l.ApplicationStopping).Returns(cancellationTokenSource.Token);

        // Stopping the real host disposes the test server, so the check is given a controllable lifetime instead.
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(new ShutdownHealthCheck(lifetime.Object));
        }));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage readyBefore = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        await cancellationTokenSource.CancelAsync();
        
        using HttpResponseMessage live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        using HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, readyBefore.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Live_WithUnhealthyReadinessCheck_ReturnsOkWhileReadyReturnsServiceUnavailable()
    {
        using var factory = CreateFactoryWithUnhealthyCheck(HealthCheckTags.Ready);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        using HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task Startup_WithUnhealthyStartupCheck_ReturnsServiceUnavailableWhileLiveAndReadyStayOk()
    {
        using var factory = CreateFactoryWithUnhealthyCheck(HealthCheckTags.Startup);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage startup = await client.GetAsync("/health/startup", TestContext.Current.CancellationToken);
        using HttpResponseMessage live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        using HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, startup.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    /// <summary>
    /// Creates a test application that registers an unhealthy health check with the specified tag.
    /// </summary>
    /// <param name="tag">The tag used to include the unhealthy check in a health endpoint.</param>
    /// <returns>A web application factory configured with the unhealthy check.</returns>
    private WebApplicationFactory<Program> CreateFactoryWithUnhealthyCheck(string tag)
    {
        return _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddHealthChecks().AddCheck(
                $"failing_{tag}_check",
                () => HealthCheckResult.Unhealthy("down"),
                tags: [tag]);
        }));
    }
}
