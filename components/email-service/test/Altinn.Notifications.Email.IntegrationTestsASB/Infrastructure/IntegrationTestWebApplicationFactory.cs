using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

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
    private WolverineSettings? _wolverineSettings;

    /// <inheritdoc/>
    protected override Dictionary<string, string?> GetFixtureConfigOverrides() => new()
    {
        ["WolverineSettings:ServiceBusConnectionString"] = Fixture.ServiceBusConnectionString,
    };

    /// <inheritdoc/>
    protected override void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services)
    {
        _wolverineSettings = configuration.GetSection("WolverineSettings").Get<WolverineSettings>()
            ?? throw new InvalidOperationException("WolverineSettings not found in configuration");

        Console.WriteLine($"[EmailFactory] ServiceBus connection: {Truncate(Fixture.ServiceBusConnectionString, 50)}...");

        RemoveServicesAssignableTo(services, typeof(KafkaConsumerBase));

        services.Replace(ServiceDescriptor.Singleton(Mock.Of<ICommonProducer>()));
    }

    /// <inheritdoc/>
    protected override async Task DrainQueuesAsync()
    {
        if (_wolverineSettings == null || !_wolverineSettings.EnableWolverine)
        {
            return;
        }

        await DrainDeadLetterQueuesAsync(
            Fixture.ServiceBusConnectionString,
            _wolverineSettings.EmailSendQueueName,
            _wolverineSettings.EmailStatusCheckQueueName);
    }
}
