using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Altinn.Notifications.Shared.Health;

/// <summary>
/// Reports unhealthy once the host signals that application shutdown is starting, so readiness probes
/// start failing while in-flight requests and background work are drained. Endpoint removal in Kubernetes
/// happens concurrently and is not awaited, so traffic may still arrive briefly after this check fails.
/// </summary>
/// <param name="lifetime">The application lifetime used to observe the host's shutdown signal.</param>
public class ShutdownHealthCheck(IHostApplicationLifetime lifetime) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        HealthCheckResult result = lifetime.ApplicationStopping.IsCancellationRequested
            ? HealthCheckResult.Unhealthy("The host has signaled that application shutdown is starting.")
            : HealthCheckResult.Healthy("The host has not signaled that application shutdown is starting.");

        return Task.FromResult(result);
    }
}
