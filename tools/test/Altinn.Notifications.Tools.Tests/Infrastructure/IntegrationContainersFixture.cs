using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

using Npgsql;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.Infrastructure;

/// <summary>
/// xUnit fixture for tools integration tests.
/// Wraps <see cref="IntegrationTestContainersFixture"/> to start PostgreSQL and the Azure Service Bus
/// emulator, then runs schema migrations so tests interact with the full database schema.
/// Shared across all test collections that declare <see cref="IntegrationContainersCollection"/>.
/// </summary>
public class IntegrationContainersFixture : IAsyncLifetime
{
    private readonly IntegrationTestContainersFixture _containers = new();

    /// <summary>Gets the Azure Service Bus emulator connection string.</summary>
    public string ServiceBusConnectionString => _containers.ServiceBusConnectionString;

    /// <summary>Gets the PostgreSQL admin connection string (already formatted — no <c>{0}</c> placeholder).</summary>
    public string PostgresConnectionString => _containers.PostgresConnectionString;

    /// <summary>Gets whether all containers started successfully.</summary>
    public bool IsRunning => _containers.IsRunning;

    /// <summary>Provides a shared <see cref="NpgsqlDataSource"/> for test data setup and assertions.</summary>
    public NpgsqlDataSource DataSource { get; private set; } = null!;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _containers.InitializeAsync();

        if (!_containers.IsRunning)
        {
            throw new InvalidOperationException(
                "Integration test containers failed to start. " +
                "Ensure Docker is running and the required images are available.");
        }

        DataSource = new NpgsqlDataSourceBuilder(PostgresConnectionString).Build();

        RunMigrations();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }

        await _containers.DisposeAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private void RunMigrations()
    {
        string migrationPath = FindMigrationPath();

        // The container's PostgresConnectionString is already fully formatted (no {0} placeholder).
        // Setting the password fields to empty makes string.Format(..., "") a safe no-op.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PostgreSQLSettings:EnableDBConnection"] = "true",
                ["PostgreSQLSettings:AdminConnectionString"] = PostgresConnectionString,
                ["PostgreSQLSettings:ConnectionString"] = PostgresConnectionString,
                ["PostgreSQLSettings:NotificationsDbAdminPwd"] = string.Empty,
                ["PostgreSQLSettings:NotificationsDbPwd"] = string.Empty,

                // Absolute path: Path.Combine ignores the CWD prefix for absolute second arguments.
                ["PostgreSQLSettings:MigrationScriptPath"] = migrationPath,
                ["PostgreSQLSettings:EnableDebug"] = "false"
            })
            .Build();

        var app = WebApplication.CreateBuilder().Build();
        app.SetUpPostgreSql(isDevelopment: false, config);
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();

        Console.WriteLine($"[IntegrationContainersFixture] Migrations applied from {migrationPath}");
    }

    /// <summary>
    /// Walks up the directory tree from the test output directory until it locates the
    /// <c>Altinn.Notifications.Persistence/Migration</c> folder.
    /// Mirrors <c>IntegrationTestWebApplicationFactory.FindMigrationPath()</c> in the API tests.
    /// </summary>
    private static string FindMigrationPath()
    {
        string? currentDir = AppContext.BaseDirectory;

        for (int i = 0; i < 12; i++)
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
            if (currentDir is null)
            {
                break;
            }

            string candidate = Path.Combine(
                currentDir,
                "components", 
                "api", 
                "src",
                "Altinn.Notifications.Persistence", 
                "Migration");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find Migration directory. " +
            $"Searched up to 12 parent directories from: {AppContext.BaseDirectory}");
    }
}
