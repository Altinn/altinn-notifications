using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Health;

/// <summary>
/// Reports healthy once the host signals that application startup has completed.
/// The host signals this after startup and hosted-service startup methods have completed.
/// </summary>
/// <param name="lifetime">The application lifetime used to observe the host's startup signal.</param>
public class StartupHealthCheck(IHostApplicationLifetime lifetime) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        HealthCheckResult result = lifetime.ApplicationStarted.IsCancellationRequested
            ? HealthCheckResult.Healthy("The host has signaled that application startup is complete.")
            : HealthCheckResult.Unhealthy("The host has not yet signaled that application startup is complete.");

        return Task.FromResult(result);
    }
}
