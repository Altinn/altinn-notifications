using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using Npgsql;

namespace Altinn.Notifications.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for API ASB integration tests.
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
        ["PostgreSQLSettings:ConnectionString"] = Fixture.PostgresConnectionString,
        ["PostgreSQLSettings:AdminConnectionString"] = Fixture.PostgresConnectionString,
        ["PostgreSQLSettings:NotificationsDbAdminPwd"] = string.Empty,
        ["PostgreSQLSettings:NotificationsDbPwd"] = string.Empty,
        ["PostgreSQLSettings:MigrationScriptPath"] = FindMigrationPath()
    };

    /// <inheritdoc/>
    protected override void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services)
    {
        WolverineSettings = configuration.GetSection("WolverineSettings").Get<WolverineSettings>()
            ?? throw new InvalidOperationException("WolverineSettings not found in configuration");

        Console.WriteLine($"[Factory] Loaded WolverineSettings - EnableWolverine: {WolverineSettings.EnableWolverine}");
        Console.WriteLine($"[Factory] ServiceBus connection: {Truncate(WolverineSettings.ServiceBusConnectionString, 50)}...");
        Console.WriteLine($"[Factory] Postgres connection: {Truncate(Fixture.PostgresConnectionString, 50)}...");

        string? uri = configuration["GeneralSettings:BaseUri"];
        if (!string.IsNullOrEmpty(uri))
        {
            ResourceLinkExtensions.Initialize(uri);
        }

        services.Replace(ServiceDescriptor.Singleton(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(Fixture.PostgresConnectionString);
            dataSourceBuilder.EnableParameterLogging(true);
            dataSourceBuilder.EnableDynamicJson();
            return dataSourceBuilder.Build();
        }));

        RemoveServicesAssignableTo(services, typeof(KafkaConsumerBase));

        services.Replace(ServiceDescriptor.Singleton(Mock.Of<IKafkaProducer>()));
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
            WolverineSettings.EmailDeliveryReportQueueName,
            WolverineSettings.EmailSendQueueName);
    }

    /// <inheritdoc/>
    protected override async Task CleanupAsync()
    {
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(Fixture.PostgresConnectionString);
            await using var cmd = dataSource.CreateCommand(
                "DELETE FROM notifications.emailnotifications; " +
                "DELETE FROM notifications.smsnotifications; " +
                "DELETE FROM notifications.orders; " +
                "DELETE FROM notifications.statusfeed; " +
                "DELETE FROM notifications.deaddeliveryreports;");
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Factory] Database cleanup failed (non-fatal): {ex.Message}");
        }
    }

    private static string FindMigrationPath()
    {
        string? currentDir = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
            if (currentDir == null)
            {
                break;
            }

            string migrationPath = Path.Combine(currentDir, "src", "Altinn.Notifications.Persistence", "Migration");
            if (Directory.Exists(migrationPath))
            {
                return migrationPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find Migration directory. Expected structure: <repo>/components/api/src/Altinn.Notifications.Persistence/Migration. " +
            $"Searched up to 10 parent directories from: {AppContext.BaseDirectory}");
    }
}
