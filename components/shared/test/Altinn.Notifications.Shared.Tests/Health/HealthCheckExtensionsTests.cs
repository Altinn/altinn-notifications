using Altinn.Notifications.Shared.Extensions;
using Altinn.Notifications.Shared.Health;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Health;

public class HealthCheckExtensionsTests
{
    private const string ConnectionString = "Endpoint=sb://127.0.0.1;SharedAccessKeyName=key;SharedAccessKey=value;UseDevelopmentEmulator=true;";

    [Fact]
    public void AddWolverineDependencyHealthChecks_RegistersListenerCheckAndOneCheckPerQueue()
    {
        var registrations = GetRegistrations(healthChecks => healthChecks.AddWolverineDependencyHealthChecks(ConnectionString, "queue.one", "queue.two"));

        string[] expectedNames =
        [
            HealthCheckExtensions.ListenerHealthCheckName,
            $"{HealthCheckExtensions.QueueHealthCheckNamePrefix}queue.one_health_check",
            $"{HealthCheckExtensions.QueueHealthCheckNamePrefix}queue.two_health_check"
        ];

        Assert.Equal(expectedNames.Order(), registrations.Select(r => r.Name).Order());
        Assert.All(registrations, r => Assert.Equal([HealthCheckTags.Dependency], r.Tags));
        Assert.All(registrations, r => Assert.Equal(TimeSpan.FromSeconds(5), r.Timeout));
    }

    [Fact]
    public void AddLifecycleHealthChecks_RegistersStartupAndShutdownChecksWithProbeTags()
    {
        var registrations = GetRegistrations(healthChecks => healthChecks.AddLifecycleHealthChecks());

        var shutdown = Assert.Single(registrations, r => r.Name == "notifications_shutdown_health_check");
        var startup = Assert.Single(registrations, r => r.Name == "notifications_startup_health_check");

        Assert.Equivalent(new[] { HealthCheckTags.Ready, HealthCheckTags.Lifecycle }, shutdown.Tags);
        Assert.Equivalent(new[] { HealthCheckTags.Startup, HealthCheckTags.Ready, HealthCheckTags.Lifecycle }, startup.Tags);
    }

    private static List<HealthCheckRegistration> GetRegistrations(Action<IHealthChecksBuilder> register)
    {
        var services = new ServiceCollection();
        register(services.AddHealthChecks());

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToList();
    }
}
