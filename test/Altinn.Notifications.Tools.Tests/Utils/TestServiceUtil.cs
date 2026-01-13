using System;
using System.Threading;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Persistence.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Altinn.Notifications.Tools.Tests.Utils;

/// <summary>
/// Simplified service utility for tests that only need persistence layer.
/// Avoids the need for full application configuration (Kafka, Platform, etc.)
/// </summary>
public static class TestServiceUtil
{
    private static readonly Lock _lock = new();
    private static NpgsqlDataSource? _sharedDataSource;
    private static IServiceProvider? _serviceProvider;
    private static bool _databaseInitialized = false;

    /// <summary>
    /// Gets repositories with minimal configuration (only PostgreSQL settings required)
    /// </summary>
    public static T GetService<T>()
        where T : notnull
    {
        EnsureServicesInitialized();
        return _serviceProvider!.GetRequiredService<T>();
    }

    /// <summary>
    /// Dispose shared resources
    /// </summary>
    public static void Dispose()
    {
        lock (_lock)
        {
            _sharedDataSource?.Dispose();
            _sharedDataSource = null;
            _serviceProvider = null;
            _databaseInitialized = false;
        }
    }

    private static void EnsureServicesInitialized()
    {
        lock (_lock)
        {
            if (_serviceProvider != null)
            {
                return;
            }

            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables();

            var config = builder.Build();

            PostgreSqlSettings? settings = config.GetSection("PostgreSQLSettings")
                .Get<PostgreSqlSettings>()
                ?? throw new InvalidOperationException("Required PostgreSQLSettings is missing from application configuration");

            // Initialize database schema if not already done
            if (!_databaseInitialized)
            {
                InitializeDatabase(config);
                _databaseInitialized = true;
            }

            string connectionString = string.Format(settings.ConnectionString, settings.NotificationsDbPwd);

            var services = new ServiceCollection();

            // Only add what we need for persistence
            services.AddLogging();

            // Register NpgsqlDataSource
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableParameterLogging(settings.LogParameters);
            dataSourceBuilder.EnableDynamicJson();
            _sharedDataSource = dataSourceBuilder.Build();
            services.AddSingleton(_sharedDataSource);

            // Register repositories
            services.AddSingleton<IOrderRepository, OrderRepository>();
            services.AddSingleton<OrderRepository>(); // Also register concrete type for StatusFeedBackfillService
            services.AddSingleton<IStatusFeedRepository, StatusFeedRepository>();
            services.AddSingleton<IEmailNotificationRepository, EmailNotificationRepository>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }

    private static void InitializeDatabase(IConfiguration config)
    {
        // Use the same migration approach as IntegrationTests
        var app = WebApplication.CreateBuilder().Build();
        app.SetUpPostgreSql(true, config);
    }
}
