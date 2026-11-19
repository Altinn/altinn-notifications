using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Health;

/// <summary>
/// Reports healthy once the host has finished starting, which happens after database migrations
/// have run and all hosted services (including the message listeners) have been started.
/// </summary>
/// <param name="lifetime">The application lifetime used to observe when startup has completed.</param>
public class StartupHealthCheck(IHostApplicationLifetime lifetime) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        HealthCheckResult result = lifetime.ApplicationStarted.IsCancellationRequested
            ? HealthCheckResult.Healthy("Application startup has completed.")
            : HealthCheckResult.Unhealthy("Application is still starting.");

        return Task.FromResult(result);
    }
}
