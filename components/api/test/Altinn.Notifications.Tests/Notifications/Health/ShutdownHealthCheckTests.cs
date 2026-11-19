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
        var sut = CreateSut(stopping.Token);

        // Act
        var result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenShutdownHasStarted_ReturnsUnhealthy()
    {
        // Arrange
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        var sut = CreateSut(stopping.Token);

        // Act
        var result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static ShutdownHealthCheck CreateSut(CancellationToken stopping)
    {
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(l => l.ApplicationStopping).Returns(stopping);
        return new ShutdownHealthCheck(lifetime.Object);
    }
}
