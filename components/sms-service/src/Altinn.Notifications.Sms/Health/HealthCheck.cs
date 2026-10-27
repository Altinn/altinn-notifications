using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Sms.Health;

/// <summary>
/// Health check for the SMS service.
/// </summary>
public class HealthCheck : IHealthCheck
{
    /// <summary>
    /// Verifies the health status.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A health check result.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("A healthy result."));
    }
}
