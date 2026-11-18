using Altinn.Notifications.Integrations.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Moq;

using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Transports;
using Wolverine.Transports.Sending;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Health;

public class WolverineMessagingHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ListenersAcceptingAndSendersNotLatched_ReturnsHealthy()
    {
        var runtime = CreateRuntime(
            listeners: [(ListeningStatus.Accepting, "asb://queue/a"), (ListeningStatus.TooBusy, "asb://queue/b")],
            senders: [(false, "asb://queue/c")]);

        var result = await Check(runtime);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_NoListenersOrSenders_ReturnsHealthy()
    {
        var result = await Check(CreateRuntime(listeners: [], senders: []));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Theory]
    [InlineData(ListeningStatus.Paused)]
    [InlineData(ListeningStatus.Stopped)]
    [InlineData(ListeningStatus.Unknown)]
    [InlineData(ListeningStatus.GloballyLatched)]
    public async Task CheckHealthAsync_ListenerNotRunning_ReturnsUnhealthyNamingTheListener(ListeningStatus status)
    {
        var runtime = CreateRuntime(
            listeners: [(ListeningStatus.Accepting, "asb://queue/ok"), (status, "asb://queue/broken")],
            senders: []);

        var result = await Check(runtime);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Listeners not running: asb://queue/broken", result.Description);
        Assert.DoesNotContain("asb://queue/ok", result.Description);
        Assert.DoesNotContain("Senders latched", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_SenderLatched_ReturnsUnhealthyNamingTheSender()
    {
        var runtime = CreateRuntime(
            listeners: [(ListeningStatus.Accepting, "asb://queue/a")],
            senders: [(false, "asb://queue/ok"), (true, "asb://queue/broken")]);

        var result = await Check(runtime);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Senders latched: asb://queue/broken", result.Description);
        Assert.DoesNotContain("asb://queue/ok", result.Description);
        Assert.DoesNotContain("Listeners not running", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ListenerAndSenderFailing_ReportsBoth()
    {
        var runtime = CreateRuntime(
            listeners: [(ListeningStatus.Stopped, "asb://queue/l")],
            senders: [(true, "asb://queue/s")]);

        var result = await Check(runtime);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Senders latched: asb://queue/s; Listeners not running: asb://queue/l", result.Description);
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
        return new WolverineMessagingHealthCheck(runtime).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
    }

    private static IWolverineRuntime CreateRuntime(
        (ListeningStatus Status, string Uri)[] listeners,
        (bool Latched, string Destination)[] senders)
    {
        var listenerAgents = listeners.Select(l =>
        {
            var agent = new Mock<IListeningAgent>();
            agent.Setup(a => a.Status).Returns(l.Status);
            agent.Setup(a => a.Uri).Returns(new Uri(l.Uri));
            return agent.Object;
        }).ToList();

        var sendingAgents = senders.Select(s =>
        {
            var agent = new Mock<ISendingAgent>();
            agent.Setup(a => a.Latched).Returns(s.Latched);
            agent.Setup(a => a.Destination).Returns(new Uri(s.Destination));
            return agent.Object;
        }).ToList();

        var endpoints = new Mock<IEndpointCollection>();
        endpoints.Setup(e => e.ActiveListeners()).Returns(listenerAgents);
        endpoints.Setup(e => e.ActiveSendingAgents()).Returns(sendingAgents);

        var runtime = new Mock<IWolverineRuntime>();
        runtime.Setup(r => r.Endpoints).Returns(endpoints.Object);
        return runtime.Object;
    }
}
