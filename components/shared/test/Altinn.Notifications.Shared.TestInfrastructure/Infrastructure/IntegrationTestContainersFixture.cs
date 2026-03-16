#nullable enable
using System.Net.Sockets;
using System.Text.Json;

using Altinn.Notifications.Shared.TestInfrastructure.Utils;

using Azure.Messaging.ServiceBus;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

/// <summary>
/// xUnit fixture that starts all required containers for integration tests (PostgreSQL, MSSQL, Azure Service Bus Emulator).
/// The fixture is shared across all tests in the collection to avoid starting/stopping containers repeatedly.
/// </summary>
public class IntegrationTestContainersFixture : IAsyncLifetime
{
    private const string _mssqlSaPassword = "YourStrong!Passw0rd";
    private INetwork? _network;
    private IContainer? _postgresContainer;
    private IContainer? _mssqlContainer;
    private IContainer? _serviceBusEmulatorContainer;

    #region Properties

    /// <summary>
    /// Gets the Azure Service Bus connection string for the emulator.
    /// </summary>
    public string ServiceBusConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the PostgreSQL connection string (admin user).
    /// </summary>
    public string PostgresConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the mapped PostgreSQL port.
    /// </summary>
    public int PostgresPort { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the containers are running.
    /// </summary>
    public bool IsRunning { get; private set; }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the fixture by starting MSSQL, Azure Service Bus Emulator, and optionally PostgreSQL containers.
    /// PostgreSQL is only started if the consuming project's appsettings.integrationtest.json contains a PostgreSQLSettings section.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _network = new NetworkBuilder()
                .WithName($"integration-test-network-{Guid.NewGuid():N}")
                .Build();

            await _network.CreateAsync();

            var postgresSettings = LoadPostgreSqlSettings();

            if (postgresSettings != null)
            {
                // Start PostgreSQL
                _postgresContainer = new ContainerBuilder(ContainerImageUtils.GetImage("postgres"))
                    .WithNetwork(_network)
                    .WithNetworkAliases("postgres")
                    .WithEnvironment("POSTGRES_USER", postgresSettings.Username)
                    .WithEnvironment("POSTGRES_PASSWORD", postgresSettings.Password)
                    .WithEnvironment("POSTGRES_DB", postgresSettings.Database)
                    .WithPortBinding(5432, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready"))
                    .WithAutoRemove(true)
                    .Build();

                await _postgresContainer.StartAsync();

                PostgresPort = _postgresContainer.GetMappedPublicPort(5432);
                PostgresConnectionString = $"Host=localhost;Port={PostgresPort};Database={postgresSettings.Database};Username={postgresSettings.Username};Password={postgresSettings.Password};SSL Mode=Disable";

                await WaitForPostgresAsync();

                Console.WriteLine($"PostgreSQL started on port {PostgresPort}");

                // Create app role for migrations
                await CreateAppRoleAsync(postgresSettings.AppRoleName, postgresSettings.AppRolePassword);
            }
            else
            {
                Console.WriteLine("PostgreSQLSettings not found in appsettings — skipping PostgreSQL container");
            }

            // Start MSSQL (required by Service Bus Emulator)
            _mssqlContainer = new ContainerBuilder(ContainerImageUtils.GetImage("mssql"))
                .WithNetwork(_network)
                .WithNetworkAliases("mssql")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("MSSQL_SA_PASSWORD", _mssqlSaPassword)
                .WithAutoRemove(true)
                .Build();

            await _mssqlContainer.StartAsync();

            string configPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "config.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Emulator config file not found at: {configPath}");
            }

            _serviceBusEmulatorContainer = new ContainerBuilder(ContainerImageUtils.GetImage("serviceBusEmulator"))
                .WithNetwork(_network)
                .WithEnvironment("SQL_SERVER", "mssql")
                .WithEnvironment("MSSQL_SA_PASSWORD", _mssqlSaPassword)
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("SQL_WAIT_INTERVAL", "5")
                .WithBindMount(configPath, "/ServiceBus_Emulator/ConfigFiles/Config.json", AccessMode.ReadOnly)
                .WithPortBinding(5672, true)
                .WithAutoRemove(true)
                .Build();

            await _serviceBusEmulatorContainer.StartAsync();

            int hostPort = _serviceBusEmulatorContainer.GetMappedPublicPort(5672);

            ServiceBusConnectionString = $"Endpoint=sb://127.0.0.1:{hostPort};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

            await WaitForServiceBusAsync();

            IsRunning = true;
            Console.WriteLine("All integration test containers started successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start integration test containers: {ex.Message}");
            IsRunning = false;
            await DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Disposes the fixture by stopping and removing all containers.
    /// </summary>
    public async Task DisposeAsync()
    {
        static async Task SafeDisposeContainerAsync(IContainer? container)
        {
            if (container == null)
            {
                return;
            }

            try
            {
                await container.StopAsync();
                await container.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing container: {ex.Message}");
            }
        }

        try
        {
            await SafeDisposeContainerAsync(_serviceBusEmulatorContainer);
            await SafeDisposeContainerAsync(_mssqlContainer);
            await SafeDisposeContainerAsync(_postgresContainer);

            if (_network != null)
            {
                await _network.DeleteAsync();
            }

            Console.WriteLine("All integration test containers cleaned up");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    #endregion

    #region Private Helpers

    private async Task WaitForPostgresAsync()
    {
        await WaitForTcpPortAsync("PostgreSQL", "127.0.0.1", PostgresPort);

        // TCP port open is not enough — probe with a real connection to ensure PostgreSQL
        // is ready to authenticate. On Windows/Docker Desktop the port can accept TCP
        // while the server is still initializing, causing auth-level failures.
        const int maxRetries = 30;
        const int delayMs = 1000;

        bool ready = await WaitForUtils.WaitForAsync(
            async () =>
            {
                try
                {
                    await using var dataSource = NpgsqlDataSource.Create(PostgresConnectionString);
                    await using var cmd = dataSource.CreateCommand("SELECT 1");
                    await cmd.ExecuteScalarAsync();
                    return true;
                }
                catch
                {
                    return false;
                }
            },
            maxRetries,
            delayMs);

        if (!ready)
        {
            throw new TimeoutException("PostgreSQL did not become ready for connections after TCP port opened");
        }

        Console.WriteLine("PostgreSQL accepting connections");
    }

    private async Task WaitForServiceBusAsync()
    {
        int hostPort = _serviceBusEmulatorContainer!.GetMappedPublicPort(5672);
        await WaitForTcpPortAsync("Service Bus Emulator", "127.0.0.1", hostPort);

        // TCP port open is not enough — the AMQP layer needs additional time to initialize.
        // Probe with a real ServiceBusClient until the emulator accepts connections.
        const int maxRetries = 30;
        const int delayMs = 1000;

        bool ready = await WaitForUtils.WaitForAsync(
            async () =>
            {
                try
                {
                    await using var client = new ServiceBusClient(ServiceBusConnectionString);
                    await using var receiver = client.CreateReceiver("smoke-test");
                    await receiver.PeekMessageAsync();
                    return true;
                }
                catch
                {
                    return false;
                }
            },
            maxRetries,
            delayMs);

        if (!ready)
        {
            throw new TimeoutException("Service Bus Emulator AMQP layer did not become ready after TCP port opened");
        }

        Console.WriteLine("Service Bus Emulator AMQP layer is ready");
    }

    private static PostgreSqlSettings? LoadPostgreSqlSettings()
    {
        string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.integrationtest.json");

        if (!File.Exists(appSettingsPath))
        {
            throw new FileNotFoundException($"appsettings.integrationtest.json not found at: {appSettingsPath}");
        }

        string json = File.ReadAllText(appSettingsPath);
        using JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("PostgreSQLSettings", out var postgresSection))
        {
            return null;
        }

        string adminConnString = postgresSection.GetProperty("AdminConnectionString").GetString()
            ?? throw new InvalidOperationException("AdminConnectionString not found");

        string adminPassword = postgresSection.GetProperty("NotificationsDbAdminPwd").GetString()
            ?? throw new InvalidOperationException("NotificationsDbAdminPwd not found");

        string appRolePassword = postgresSection.GetProperty("NotificationsDbPwd").GetString()
            ?? throw new InvalidOperationException("NotificationsDbPwd not found");

        var builder = new NpgsqlConnectionStringBuilder(adminConnString);
        string database = builder.Database
            ?? throw new InvalidOperationException("Database not found in AdminConnectionString");
        string username = builder.Username
            ?? throw new InvalidOperationException("Username not found in AdminConnectionString");

        return new PostgreSqlSettings(database, username, adminPassword, "platform_notifications", appRolePassword);
    }

    /// <summary>
    /// Creates the application database role required for migrations.
    /// </summary>
    private async Task CreateAppRoleAsync(string roleName, string password)
    {
        try
        {
            Console.WriteLine($"Creating {roleName} database role...");

            await using var dataSource = NpgsqlDataSource.Create(PostgresConnectionString);
            await using var command = dataSource.CreateCommand($@"
                DO $$
                BEGIN
                    CREATE ROLE {roleName} WITH LOGIN PASSWORD '{password}';
                    RAISE NOTICE 'Role {roleName} created successfully';
                EXCEPTION
                    WHEN duplicate_object THEN
                        RAISE NOTICE 'Role {roleName} already exists, skipping';
                END
                $$;");
            await command.ExecuteNonQueryAsync();

            Console.WriteLine($"Created {roleName} database role");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create {roleName} role: {ex.Message}");
            throw;
        }
    }

    private static async Task WaitForTcpPortAsync(string serviceName, string host, int port)
    {
        const int maxRetries = 30;
        const int delayMs = 1000;
        const int connectTimeoutMs = 1000;

        int attemptNumber = 0;
        bool ready = await WaitForUtils.WaitForAsync(
            async () =>
            {
                attemptNumber++;
                try
                {
                    using var client = new TcpClient();
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(connectTimeoutMs));
                    await client.ConnectAsync(host, port, cts.Token);

                    Console.WriteLine($"{serviceName} ready after {attemptNumber} attempt(s)");
                    return true;
                }
                catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                {
                    Console.WriteLine($"Waiting for {serviceName}... attempt {attemptNumber}/{maxRetries}");
                    return false;
                }
            },
            maxRetries,
            delayMs);

        if (!ready)
        {
            throw new TimeoutException($"{serviceName} did not become ready after {maxRetries} attempts");
        }
    }

    #endregion
}

/// <summary>
/// PostgreSQL settings loaded from appsettings.
/// </summary>
internal record PostgreSqlSettings(string Database, string Username, string Password, string AppRoleName, string AppRolePassword);
