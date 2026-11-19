using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Health;

/// <summary>
/// Reports unhealthy as soon as the host begins shutting down, so readiness probes start failing while
/// in-flight requests and background work are drained. Endpoint removal in Kubernetes happens
/// concurrently and is not awaited, so traffic may still arrive briefly after this check fails.
/// </summary>
/// <param name="lifetime">The application lifetime used to observe when shutdown has started.</param>
public class ShutdownHealthCheck(IHostApplicationLifetime lifetime) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        HealthCheckResult result = lifetime.ApplicationStopping.IsCancellationRequested
            ? HealthCheckResult.Unhealthy("Application is shutting down.")
            : HealthCheckResult.Healthy("Application is not shutting down.");

        return Task.FromResult(result);
    }
}
