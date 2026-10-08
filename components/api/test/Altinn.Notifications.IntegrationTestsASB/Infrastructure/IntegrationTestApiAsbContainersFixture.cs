using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

namespace Altinn.Notifications.IntegrationTestsASB.Infrastructure;

public class IntegrationTestApiAsbContainersFixture : IntegrationTestContainersFixture
{
    /// <summary>
    /// Gets the shared API ASB test host instance.
    /// </summary>
    public IntegrationTestWebApplicationFactory WebHost { get; private set; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        WebHost = new IntegrationTestWebApplicationFactory(this)
            .Initialize();
    }

    public override async ValueTask DisposeAsync()
    {
        if (WebHost != null)
        {
            await WebHost.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    /// <summary>
    /// Resets runtime-installed service overrides for the shared host.
    /// </summary>
    internal void ResetInstalledMocks()
    {
        WebHost.ResetInstalledMocks();
    }

    /// <summary>
    /// Installs a service override used by the shared host for subsequent test operations.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    /// <param name="service">Service instance to install.</param>
    internal void InstallService<TService>(TService service)
        where TService : class
    {
        WebHost.InstallService(service);
    }

    /// <summary>
    /// Drains both the main queue and its dead-letter queue.
    /// </summary>
    /// <param name="queueName">Queue name to drain.</param>
    internal async Task DrainQueueAsync(string queueName)
    {
        await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, queueName);
        await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, $"{queueName}/$deadletterqueue");
    }
}
