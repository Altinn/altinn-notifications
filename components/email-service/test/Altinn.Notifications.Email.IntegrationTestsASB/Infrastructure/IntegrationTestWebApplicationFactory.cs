using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for email service ASB integration tests.
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

        Console.WriteLine($"[EmailFactory] ServiceBus connection: {Truncate(Fixture.ServiceBusConnectionString, 50)}...");
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
            WolverineSettings.EmailSendQueueName,
            WolverineSettings.EmailSendResultQueueName,
            WolverineSettings.EmailStatusCheckQueueName,
            WolverineSettings.EmailServiceRateLimitQueueName);
    }
}
