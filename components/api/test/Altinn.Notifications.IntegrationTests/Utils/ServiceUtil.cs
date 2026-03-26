using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Integrations.Extensions;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Extensions;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class ServiceUtil
{
    private static readonly object _lock = new();
    private static NpgsqlDataSource? _sharedDataSource;

    private static bool _migrationsRun;
    private static readonly object _migrationLock = new();
    private static IConfiguration? _defaultConfig;
    private static ServiceProvider? _defaultServiceProvider;

    public static NpgsqlDataSource SharedDataSource
    {
        get
        {
            var config = GetOrCreateDefaultConfig();
            EnsureMigrationsRun(config);
            return GetOrCreateDataSource(config);
        }
    }

    private static NpgsqlDataSource GetOrCreateDataSource(IConfiguration config)
    {
        lock (_lock)
        {
            if (_sharedDataSource != null)
            {
                return _sharedDataSource;
            }

            PostgreSqlSettings? settings = config.GetSection("PostgreSQLSettings")
                .Get<PostgreSqlSettings>()
                ?? throw new ArgumentNullException(nameof(config), "Required PostgreSQLSettings is missing from application configuration");

            string connectionString = string.Format(settings.ConnectionString, settings.NotificationsDbPwd);

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableParameterLogging(settings.LogParameters);
            dataSourceBuilder.EnableDynamicJson();

            // Note: Tracing configuration (ConfigureTracing) is intentionally omitted in tests
            // to reduce noise and overhead. Tests focus on functional correctness rather than observability.
            _sharedDataSource = dataSourceBuilder.Build();

            return _sharedDataSource;
        }
    }

    public static void DisposeSharedDataSource()
    {
        lock (_lock)
        {
            _sharedDataSource?.Dispose();
            _sharedDataSource = null;

            _defaultServiceProvider?.Dispose();
            _defaultServiceProvider = null;
        }
    }

    public static List<object> GetServices(List<Type> interfaceTypes, Dictionary<string, string>? envVariables = null)
    {
        ServiceProvider serviceProvider;

        if (envVariables == null)
        {
            serviceProvider = GetOrCreateDefaultProvider();
        }
        else
        {
            foreach (var item in envVariables)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }

            var config = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json")
                .AddJsonFile("appsettings.IntegrationTest.json")
                .AddEnvironmentVariables()
                .Build();

            EnsureMigrationsRun(config);
            serviceProvider = BuildServiceProvider(config);
        }

        List<object> outputServices = new();

        foreach (Type interfaceType in interfaceTypes)
        {
            var outputServiceObject = serviceProvider.GetServices(interfaceType)!;
            outputServices.AddRange(outputServiceObject!);
        }

        return outputServices;
    }

    private static IConfiguration GetOrCreateDefaultConfig()
    {
        if (_defaultConfig != null)
        {
            return _defaultConfig;
        }

        _defaultConfig = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json")
            .AddEnvironmentVariables()
            .Build();

        return _defaultConfig;
    }

    private static void EnsureMigrationsRun(IConfiguration config)
    {
        if (_migrationsRun)
        {
            return;
        }

        lock (_migrationLock)
        {
            if (_migrationsRun)
            {
                return;
            }

            WebApplication.CreateBuilder()
                           .Build()
                           .SetUpPostgreSql(true, config);

            _migrationsRun = true;
        }
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration config)
    {
        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment
        {
            EnvironmentName = config["ASPNETCORE_ENVIRONMENT"] ?? "IntegrationTest",
            ApplicationName = "Altinn.Notifications.IntegrationTests",
            ContentRootPath = Directory.GetCurrentDirectory()
        });
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddLogging();

        // Register the shared data source as a singleton
        var sharedDataSource = GetOrCreateDataSource(config);
        services.AddSingleton(sharedDataSource);

        // Register all repository implementations using the shared data source
        RegisterRepositories(services);

        services.AddCoreServices(config);
        services.AddKafkaServices(config);
        services.AddAltinnClients(config);
        services.AddAuthorizationService(config);

        return services.BuildServiceProvider();
    }

    private static ServiceProvider GetOrCreateDefaultProvider()
    {
        if (_defaultServiceProvider != null)
        {
            return _defaultServiceProvider;
        }

        var config = GetOrCreateDefaultConfig();
        EnsureMigrationsRun(config);
        _defaultServiceProvider = BuildServiceProvider(config);

        return _defaultServiceProvider;
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        // Explicitly register repositories to match production configuration.
        // This provides compile-time safety and avoids fragility of reflection-based registration.
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<IMetricsRepository, MetricsRepository>();
        services.AddSingleton<IStatusFeedRepository, StatusFeedRepository>();
        services.AddSingleton<IResourceLimitRepository, ResourceLimitRepository>();
        services.AddSingleton<ISmsNotificationRepository, SmsNotificationRepository>();
        services.AddSingleton<IEmailNotificationRepository, EmailNotificationRepository>();
        services.AddSingleton<INotificationSummaryRepository, NotificationSummaryRepository>();
        services.AddSingleton<INotificationDeliveryManifestRepository, NotificationDeliveryManifestRepository>();
        services.AddSingleton<IDeadDeliveryReportRepository, DeadDeliveryReportRepository>();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
