using Altinn.Notifications.Shared.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

/// <summary>
/// Service collection helpers for integration test hosts.
/// </summary>
public static class HealthCheckTestExtensions
{
    /// <summary>
    /// Removes the Wolverine listener and Azure Service Bus queue health checks.
    /// Test hosts strip the Wolverine runtime and the Service Bus transport, so these checks cannot run.
    /// </summary>
    /// <param name="services">The service collection to modify.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection RemoveBrokerHealthChecks(this IServiceCollection services)
    {
        return services.Configure<HealthCheckServiceOptions>(options =>
        {
            options.Registrations
                .Where(r => r.Name == HealthCheckExtensions.ListenerHealthCheckName || r.Name.StartsWith(HealthCheckExtensions.QueueHealthCheckNamePrefix))
                .ToList()
                .ForEach(r => options.Registrations.Remove(r));
        });
    }
}
