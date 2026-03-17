using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Azure.Messaging.ServiceBus;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Moq;

using Npgsql;

namespace Altinn.Notifications.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for ASB integration tests that uses the real Program.cs setup
/// with test-specific overrides via in-memory configuration.
/// </summary>
public class IntegrationTestWebApplicationFactory(IntegrationTestContainersFixture fixture) : WebApplicationFactory<Program>
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private IHost _host = null!;
    private readonly List<Action<IServiceCollection>> _configureTestServices = [];
    private readonly Dictionary<string, string?> _configOverrides = [];

    /// <summary>
    /// Gets the Wolverine settings loaded from configuration.
    /// </summary>
    public WolverineSettings WolverineSettings { get; private set; } = null!;

    /// <summary>
    /// Gets the IHost instance for use with Wolverine's IMessageBus.
    /// Access this after calling CreateClient() or Initialize().
    /// </summary>
    public IHost Host => _host ?? throw new InvalidOperationException("Host not created yet. Call CreateClient() or Initialize() first.");

    /// <summary>
    /// Configures additional test services. Use this to replace services with mocks.
    /// Must be called before CreateClient().
    /// </summary>
    public IntegrationTestWebApplicationFactory ConfigureTestServices(Action<IServiceCollection> configure)
    {
        _configureTestServices.Add(configure);
        return this;
    }

    /// <summary>
    /// Adds an in-memory configuration override, applied after appsettings.integrationtest.json.
    /// Use this to enable or disable specific settings per test.
    /// Must be called before CreateClient().
    /// </summary>
    public IntegrationTestWebApplicationFactory WithConfig(string key, string? value)
    {
        _configOverrides[key] = value;
        return this;
    }

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.integrationtest.json", optional: false, reloadOnChange: false);

            var testConfigOverrides = new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = _fixture.ServiceBusConnectionString,
                ["PostgreSQLSettings:ConnectionString"] = _fixture.PostgresConnectionString,
                ["PostgreSQLSettings:AdminConnectionString"] = _fixture.PostgresConnectionString,
                ["PostgreSQLSettings:NotificationsDbAdminPwd"] = string.Empty,
                ["PostgreSQLSettings:NotificationsDbPwd"] = string.Empty,
                ["PostgreSQLSettings:MigrationScriptPath"] = FindMigrationPath()
            };
            foreach (var (key, value) in _configOverrides)
            {
                testConfigOverrides[key] = value;
            }

            config.AddInMemoryCollection(testConfigOverrides);
        });

        builder.ConfigureServices((context, services) =>
        {
            WolverineSettings = context.Configuration.GetSection("WolverineSettings").Get<WolverineSettings>()
                ?? throw new InvalidOperationException("WolverineSettings not found in configuration");

            Console.WriteLine($"[Factory] Loaded WolverineSettings - EnableWolverine: {WolverineSettings.EnableWolverine}");
            Console.WriteLine($"[Factory] ServiceBus connection: {Truncate(WolverineSettings.ServiceBusConnectionString, 50)}...");
            Console.WriteLine($"[Factory] Postgres connection: {Truncate(_fixture.PostgresConnectionString, 50)}...");

            // Override initialization of extension class with test settings
            string? uri = context.Configuration["GeneralSettings:BaseUri"];
            if (!string.IsNullOrEmpty(uri))
            {
                ResourceLinkExtensions.Initialize(uri);
            }

            // Replace NpgsqlDataSource with test container connection
            services.Replace(ServiceDescriptor.Singleton(sp =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(_fixture.PostgresConnectionString);
                dataSourceBuilder.EnableParameterLogging(true);
                dataSourceBuilder.EnableDynamicJson();
                return dataSourceBuilder.Build();
            }));

            // Remove all Kafka services - they are not needed in ASB tests
            // and the KafkaProducer constructor crashes without a running broker
            var consumersToRemove = services
                .Where(s => s.ImplementationType?.IsAssignableTo(typeof(KafkaConsumerBase)) == true)
                .ToList();

            foreach (var descriptor in consumersToRemove)
            {
                services.Remove(descriptor);
            }

            services.Replace(ServiceDescriptor.Singleton(Mock.Of<IKafkaProducer>()));

            // Apply any additional test service configuration
            foreach (var configure in _configureTestServices)
            {
                configure(services);
            }
        });

        builder.UseEnvironment("Development");

        _host = base.CreateHost(builder);
        return _host;
    }

    private static string Truncate(string? value, int maxLength) =>
        value is null ? "(null)" : value[..Math.Min(value.Length, maxLength)];

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

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        // Give Wolverine's Service Bus processors time to settle before disposal
        await Task.Delay(150);

        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await CleanupDatabaseAsync();
            await DrainAllDeadLetterQueuesAsync();
            GC.SuppressFinalize(this);
        }
    }

    private async Task CleanupDatabaseAsync()
    {
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(_fixture.PostgresConnectionString);
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

    private async Task DrainAllDeadLetterQueuesAsync()
    {
        if (WolverineSettings == null || !WolverineSettings.EnableWolverine)
        {
            return;
        }

        string[] queueNames = [WolverineSettings.EmailDeliveryReportQueueName];
        queueNames = Array.FindAll(queueNames, n => !string.IsNullOrWhiteSpace(n));

        try
        {
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);

            foreach (var queueName in queueNames)
            {
                await using var receiver = client.CreateReceiver($"{queueName}/$deadletterqueue");

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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Factory] DLQ drain failed (non-fatal): {ex.Message}");
        }
    }
}
