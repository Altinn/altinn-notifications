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
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(l => l.ApplicationStarted).Returns(started.Token);
        var sut = new StartupHealthCheck(lifetime.Object);

        // Act
        var result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_AfterApplicationStarted_ReturnsHealthy()
    {
        // Arrange
        using var started = new CancellationTokenSource();
        await started.CancelAsync();
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(l => l.ApplicationStarted).Returns(started.Token);
        var sut = new StartupHealthCheck(lifetime.Object);

        // Act
        var result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
