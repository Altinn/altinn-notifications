using Altinn.Notifications.Shared.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.HealthChecks;

namespace Altinn.Notifications.Shared.Extensions;

/// <summary>
/// Extension methods for the startup, readiness and liveness health checks shared by the notification services.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// The name of the check that reports the Wolverine listener state.
    /// </summary>
    public const string ListenerHealthCheckName = "notifications_wolverine_health_check";

    /// <summary>
    /// The prefix of the name of the checks that probe the Azure Service Bus queues.
    /// </summary>
    public const string QueueHealthCheckNamePrefix = "notifications_service_bus_";

    /// <summary>
    /// Registers the startup and shutdown lifecycle checks, which gate <c>/health/startup</c> and <c>/health/ready</c>.
    /// </summary>
    /// <param name="healthChecks">The health checks builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHealthChecksBuilder AddLifecycleHealthChecks(this IHealthChecksBuilder healthChecks)
    {
        return healthChecks
            .AddCheck<ShutdownHealthCheck>("notifications_shutdown_health_check", tags: [HealthCheckTags.Ready, HealthCheckTags.Lifecycle])
            .AddCheck<StartupHealthCheck>("notifications_startup_health_check", tags: [HealthCheckTags.Startup, HealthCheckTags.Ready, HealthCheckTags.Lifecycle]);
    }

    /// <summary>
    /// Registers dependency checks for the Wolverine listeners and for each Azure Service Bus queue that the service listens to.
    /// The checks do not affect <c>/health/ready</c>.
    /// </summary>
    /// <param name="healthChecks">The health checks builder.</param>
    /// <param name="serviceBusConnectionString">The Azure Service Bus connection string.</param>
    /// <param name="listenQueueNames">The names of the queues the service listens to.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHealthChecksBuilder AddWolverineDependencyHealthChecks(this IHealthChecksBuilder healthChecks, string serviceBusConnectionString, params string[] listenQueueNames)
    {
        TimeSpan timeout = TimeSpan.FromSeconds(5);
        string[] tags = [HealthCheckTags.Dependency];

        // Checks Wolverine's in-process listener state, which does not reflect a broker outage.
        healthChecks.AddWolverineListeners(name: ListenerHealthCheckName, tags: tags, timeout: timeout);

        // Checks each queue directly against the broker.
        foreach (string queueName in listenQueueNames)
        {
            healthChecks.AddAzureServiceBusQueue(
                serviceBusConnectionString,
                queueName,
                name: $"{QueueHealthCheckNamePrefix}{queueName}_health_check",
                tags: tags,
                timeout: timeout);
        }

        return healthChecks;
    }

    /// <summary>
    /// Maps <c>/health</c>, <c>/health/live</c>, <c>/health/startup</c> and <c>/health/ready</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapNotificationsHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health");

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthCheckTags.Live)
        });

        endpoints.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthCheckTags.Startup)
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthCheckTags.Ready)
        });

        return endpoints;
    }
}
