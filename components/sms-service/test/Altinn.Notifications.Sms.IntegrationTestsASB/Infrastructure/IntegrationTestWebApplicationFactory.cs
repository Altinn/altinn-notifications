using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Consumers;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for SMS service ASB integration tests.
/// Boots the real Program.cs with test-specific configuration and service overrides.
/// </summary>
public class IntegrationTestWebApplicationFactory(IntegrationTestContainersFixture fixture)
    : IntegrationTestWebApplicationFactoryBase<Program, IntegrationTestWebApplicationFactory>(fixture)
{
    /// <summary>
    /// Gets the Wolverine settings loaded from configuration
    /// </summary>
    public WolverineSettings WolverineSettings { get; private set; } = null!;

    /// <inheritdoc/>
    protected override Dictionary<string, string?> GetFixtureConfigOverrides() => new()
    {
        ["WolverineSettings:ServiceBusConnectionString"] = Fixture.ServiceBusConnectionString,
    };

    /// <inheritdoc/>
    protected override void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services)
    {
        Console.WriteLine($"[SmsFactory] ServiceBus connection: {Truncate(Fixture.ServiceBusConnectionString, 50)}...");

        WolverineSettings = configuration.GetSection("WolverineSettings").Get<WolverineSettings>()
           ?? throw new InvalidOperationException("WolverineSettings not found in configuration");

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
            Console.WriteLine($"[SmsFactory] DLQ drain failed (non-fatal): {ex.Message}");
        }
    }
}
