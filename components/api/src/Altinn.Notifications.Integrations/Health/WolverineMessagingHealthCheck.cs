using Microsoft.Extensions.Diagnostics.HealthChecks;

using Wolverine.Runtime;
using Wolverine.Transports;

namespace Altinn.Notifications.Integrations.Health;

/// <summary>
/// Health check confirming that the Wolverine listeners and senders for Azure Service Bus are running.
/// It reflects Wolverine's in-process state and does not call Azure Service Bus.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WolverineMessagingHealthCheck"/> class.
/// </remarks>
public class WolverineMessagingHealthCheck(IWolverineRuntime runtime) : IHealthCheck
{
    private readonly IWolverineRuntime _runtime = runtime;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var sendersLatched = _runtime.Endpoints
                .ActiveSendingAgents()
                .Where(sender => sender.Latched)
                .Select(sender => sender.Destination.ToString())
                .ToList();

            var listenersNotRunning = _runtime.Endpoints
                .ActiveListeners()
                .Where(listener => listener.Status is not (ListeningStatus.Accepting or ListeningStatus.TooBusy))
                .Select(listener => listener.Uri.ToString())
                .ToList();

            if (listenersNotRunning.Count == 0 && sendersLatched.Count == 0)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }

            var problems = new List<string>();
            if (sendersLatched.Count > 0)
            {
                problems.Add($"Senders latched: {string.Join(", ", sendersLatched)}");
            }

            if (listenersNotRunning.Count > 0)
            {
                problems.Add($"Listeners not running: {string.Join(", ", listenersNotRunning)}");
            }

            return Task.FromResult(HealthCheckResult.Unhealthy(string.Join("; ", problems)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(exception: ex));
        }
    }
}
