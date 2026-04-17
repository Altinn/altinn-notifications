using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Consumers;

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
    /// Gets the Wolverine settings loaded from configuration.
    /// </summary>
    public WolverineSettings? WolverineSettings { get; private set; }

    /// <inheritdoc/>
    protected override Dictionary<string, string?> GetFixtureConfigOverrides() => new()
    {
        ["WolverineSettings:ServiceBusConnectionString"] = Fixture.ServiceBusConnectionString,
    };

    /// <inheritdoc/>
    protected override void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services)
    {
        WolverineSettings = configuration.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>()
            ?? throw new InvalidOperationException(
                "Missing WolverineSettings configuration for ASB integration tests.");

        Console.WriteLine($"[SmsFactory] ServiceBus connection: {Truncate(Fixture.ServiceBusConnectionString, 50)}...");

        RemoveServicesAssignableTo(services, typeof(KafkaConsumerBase));

        services.Replace(ServiceDescriptor.Singleton(Mock.Of<ICommonProducer>()));
    }

    /// <inheritdoc/>
    protected override async Task DrainQueuesAsync()
    {
        if (WolverineSettings == null || !WolverineSettings.EnableWolverine)
        {
            return;
        }

        await DrainDeadLetterQueuesAsync(
            Fixture.ServiceBusConnectionString, 
            WolverineSettings.SmsDeliveryReportQueueName);
    }
}
