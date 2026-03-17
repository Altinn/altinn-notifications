using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for email service ASB integration tests.
/// Boots the real Program.cs with test-specific configuration and service overrides.
/// </summary>
public class IntegrationTestWebApplicationFactory(IntegrationTestContainersFixture fixture)
    : IntegrationTestWebApplicationFactoryBase<Program, IntegrationTestWebApplicationFactory>(fixture)
{
    /// <inheritdoc/>
    protected override Dictionary<string, string?> GetFixtureConfigOverrides() => new()
    {
        ["WolverineSettings:ServiceBusConnectionString"] = Fixture.ServiceBusConnectionString,
    };

    /// <inheritdoc/>
    protected override void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services)
    {
        Console.WriteLine($"[EmailFactory] ServiceBus connection: {Truncate(Fixture.ServiceBusConnectionString, 50)}...");

        var consumersToRemove = services
            .Where(s => s.ImplementationType?.IsAssignableTo(typeof(KafkaConsumerBase)) == true)
            .ToList();

        foreach (var descriptor in consumersToRemove)
        {
            services.Remove(descriptor);
        }

        services.Replace(ServiceDescriptor.Singleton(Mock.Of<ICommonProducer>()));
    }

    /// <inheritdoc/>
    protected override async Task DrainQueuesAsync()
    {
        try
        {
            await using var client = new ServiceBusClient(Fixture.ServiceBusConnectionString);
            await using var receiver = client.CreateReceiver("smoke-test/$deadletterqueue");

            while (true)
            {
                var message = await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(500));
                if (message == null)
                {
                    break;
                }

                await receiver.CompleteMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailFactory] DLQ drain failed (non-fatal): {ex.Message}");
        }
    }
}
