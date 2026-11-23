using Altinn.Notifications.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Health;

public class StartupHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_BeforeApplicationStarted_ReturnsUnhealthy()
    {
        // Arrange
        using var started = new CancellationTokenSource();
        var startupHealthCheck = CreateStartupHealthCheck(started.Token);

        // Act
        var result = await startupHealthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_AfterApplicationStarted_ReturnsHealthy()
    {
        // Arrange
        using var started = new CancellationTokenSource();
        await started.CancelAsync();
        var startupHealthCheck = CreateStartupHealthCheck(started.Token);

        // Act
        var result = await startupHealthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// Creates a startup health check with the specified application-started signal.
    /// </summary>
    /// <param name="started">The token representing whether the host has completed startup.</param>
    /// <returns>A startup health check backed by a mocked application lifetime.</returns>
    private static StartupHealthCheck CreateStartupHealthCheck(CancellationToken started)
    {
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(e => e.ApplicationStarted).Returns(started);
        return new StartupHealthCheck(lifetime.Object);
    }
}
