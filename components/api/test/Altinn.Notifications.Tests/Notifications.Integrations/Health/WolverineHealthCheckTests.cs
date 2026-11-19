using Altinn.Notifications.Integrations.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Moq;

using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Transports;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Health;

public class WolverineHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ListenersAcceptingOrTooBusy_ReturnsHealthy()
    {
        var runtime = CreateRuntime([(ListeningStatus.Accepting, "asb://queue/a"), (ListeningStatus.TooBusy, "asb://queue/b")]);

        var result = await Check(runtime);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_NoListeners_ReturnsHealthy()
    {
        var result = await Check(CreateRuntime([]));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Theory]
    [InlineData(ListeningStatus.Paused)]
    [InlineData(ListeningStatus.Stopped)]
    [InlineData(ListeningStatus.Unknown)]
    [InlineData(ListeningStatus.GloballyLatched)]
    public async Task CheckHealthAsync_ListenerNotRunning_ReturnsUnhealthyNamingTheListener(ListeningStatus status)
    {
        var runtime = CreateRuntime([(ListeningStatus.Accepting, "asb://queue/ok"), (status, "asb://queue/broken")]);

        var result = await Check(runtime);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Listeners not running: asb://queue/broken", result.Description);
        Assert.DoesNotContain("asb://queue/ok", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_RuntimeThrows_ReturnsUnhealthyWithException()
    {
        var result = await Check(null!);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    private static Task<HealthCheckResult> Check(IWolverineRuntime runtime)
    {
        return new WolverineHealthCheck(runtime).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
    }

    private static IWolverineRuntime CreateRuntime((ListeningStatus Status, string Uri)[] listeners)
    {
        var listenerAgents = listeners.Select(l =>
        {
            var agent = new Mock<IListeningAgent>();
            agent.Setup(a => a.Status).Returns(l.Status);
            agent.Setup(a => a.Uri).Returns(new Uri(l.Uri));
            return agent.Object;
        }).ToList();

        var endpoints = new Mock<IEndpointCollection>();
        endpoints.Setup(e => e.ActiveListeners()).Returns(listenerAgents);

        var runtime = new Mock<IWolverineRuntime>();
        runtime.Setup(r => r.Endpoints).Returns(endpoints.Object);
        return runtime.Object;
    }
}
