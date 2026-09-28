using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Shared.TestInfrastructure.Utils;

namespace Altinn.Notifications.IntegrationTestsASB.Infrastructure;

public class IntegrationTestApiAsbContainersFixture : IntegrationTestContainersFixture
{
    public IntegrationTestWebApplicationFactory WebHost { get; private set; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        WebHost = new IntegrationTestWebApplicationFactory(this)
            .Initialize();
    }

    public override async ValueTask DisposeAsync()
    {
        await WebHost.DisposeAsync();
        await base.DisposeAsync();
    }

    internal void ResetInstalledMocks()
    {
        WebHost.ResetInstalledMocks();
    }

    internal void InstallService<TService>(TService service)
        where TService : class
    {
        WebHost.InstallService(service);
    }

    internal async Task DrainQueue(string queueName)
    {
        await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, queueName);
        await ServiceBusTestUtils.DrainQueueAsync(ServiceBusConnectionString, $"{queueName}/$deadletterqueue");
    }
}
