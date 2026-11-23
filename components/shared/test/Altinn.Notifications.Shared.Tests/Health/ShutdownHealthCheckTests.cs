using Altinn.Notifications.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Health;

public class ShutdownHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_BeforeShutdown_ReturnsHealthy()
    {
        // Arrange
        using var stopping = new CancellationTokenSource();
        var shutdownHealthCheck = CreateShutdownHealthCheck(stopping.Token);

        // Act
        var result = await shutdownHealthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("The host has not signaled that application shutdown is starting.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenShutdownHasStarted_ReturnsUnhealthy()
    {
        // Arrange
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        var shutdownHealthCheck = CreateShutdownHealthCheck(stopping.Token);

        // Act
        var result = await shutdownHealthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    /// <summary>
    /// Creates a shutdown health check with the specified application-stopping signal.
    /// </summary>
    /// <param name="stopping">The token representing whether the host has started shutting down.</param>
    /// <returns>A shutdown health check backed by a mocked application lifetime.</returns>
    private static ShutdownHealthCheck CreateShutdownHealthCheck(CancellationToken stopping)
    {
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(e => e.ApplicationStopping).Returns(stopping);
        return new ShutdownHealthCheck(lifetime.Object);
    }
}
