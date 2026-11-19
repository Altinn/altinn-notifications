using Microsoft.Extensions.Diagnostics.HealthChecks;

using Wolverine.Runtime;
using Wolverine.Transports;

namespace Altinn.Notifications.Integrations.Health;

/// <summary>
/// Health check confirming that every active Wolverine listener is running.
/// It reflects Wolverine's in-process state only and does not contact Azure Service Bus;
/// connectivity is verified by <see cref="ServiceBusHealthCheck"/>.
/// </summary>
/// <remarks>
/// Sending agents are not inspected. All senders use inline sending, for which Wolverine always reports
/// the <c>Latched</c> state of the sending agent as <c>false</c>.
/// </remarks>
/// <param name="runtime">The Wolverine runtime that exposes the active listeners.</param>
public class WolverineHealthCheck(IWolverineRuntime runtime) : IHealthCheck
{
    private readonly IWolverineRuntime _runtime = runtime;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var listenersNotRunning = _runtime.Endpoints
                .ActiveListeners()
                .Where(listener => listener.Status is not (ListeningStatus.Accepting or ListeningStatus.TooBusy))
                .Select(listener => listener.Uri.ToString())
                .ToList();

            HealthCheckResult result = listenersNotRunning.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Listeners not running: {string.Join(", ", listenersNotRunning)}");

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(exception: ex));
        }
    }
}
