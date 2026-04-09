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
    private static bool _postgreSqlInitialized;
    private static NpgsqlDataSource? _sharedDataSource;
    private static ServiceProvider? _sharedServiceProvider;

    // Tracks every custom config ServiceProvider created for env-variable overrides.
    // Disposed together during teardown to avoid leaking IDisposable singletons.
    private static readonly List<ServiceProvider> _customConfigProviders = [];

    public static NpgsqlDataSource GetSharedDataSource()
    {
        lock (_lock)
        {
            if (_sharedDataSource != null)
            {
                return _sharedDataSource;
            }

            var config = BuildConfiguration();
            _sharedDataSource = CreateDataSource(config);
            return _sharedDataSource;
        }
    }

    private static NpgsqlDataSource CreateDataSource(IConfiguration config)
    {
        PostgreSqlSettings? settings = config.GetSection("PostgreSQLSettings")
            .Get<PostgreSqlSettings>()
            ?? throw new ArgumentNullException(nameof(config), "Required PostgreSQLSettings is missing from application configuration");

        string connectionString = string.Format(settings.ConnectionString, settings.NotificationsDbPwd);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableParameterLogging(settings.LogParameters);
        dataSourceBuilder.EnableDynamicJson();

        return dataSourceBuilder.Build();
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string>? envVariables = null)
    {
        if (envVariables != null)
        {
            foreach (var item in envVariables)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }

        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json")
            .AddEnvironmentVariables()
            .Build();
    }

    public static void DisposeSharedDataSource()
    {
        lock (_lock)
        {
            // Dispose custom config providers first — they may hold scoped resources.
            foreach (var sp in _customConfigProviders)
            {
                sp.Dispose();
            }

            _customConfigProviders.Clear();

            _sharedServiceProvider?.Dispose();
            _sharedServiceProvider = null;
            _sharedDataSource?.Dispose();
            _sharedDataSource = null;
            _postgreSqlInitialized = false;
        }
    }

    public static List<object> GetServices(List<Type> interfaceTypes, Dictionary<string, string>? envVariables = null)
    {
        if (envVariables is { Count: > 0 })
        {
            return BuildServiceProvider(envVariables, interfaceTypes);
        }

        // Otherwise, reuse the shared provider
        lock (_lock)
        {
            _sharedServiceProvider ??= BuildSharedServiceProvider();
        }

        List<object> outputServices = [];
        foreach (Type interfaceType in interfaceTypes)
        {
            outputServices.AddRange(_sharedServiceProvider.GetServices(interfaceType)!);
        }

        var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json")
            .AddEnvironmentVariables();

        var config = builder.Build();
        return outputServices;
    }

    /// <summary>
    /// Runs Yuniql database migrations at most once per test run. The
    /// PostgreSQL settings are identical regardless of Kafka topic overrides,
    /// so a single invocation is sufficient.
    /// </summary>
    private static void EnsurePostgreSqlSetup(IConfiguration config)
    {
        if (_postgreSqlInitialized)
        {
            return;
        }

        lock (_lock)
        {
            if (_postgreSqlInitialized)
            {
                return;
            }

            WebApplication.CreateBuilder()
                           .Build()
                           .SetUpPostgreSql(true, config);

            _postgreSqlInitialized = true;
        }
    }

    private static ServiceProvider BuildSharedServiceProvider()
    {
        var config = BuildConfiguration();
        EnsurePostgreSqlSetup(config);

        IServiceCollection services = new ServiceCollection();
        ConfigureServices(services, config);
        return services.BuildServiceProvider();
    }

    private static List<object> BuildServiceProvider(Dictionary<string, string> envVariables, List<Type> interfaceTypes)
    {
        var config = BuildConfiguration(envVariables);
        EnsurePostgreSqlSetup(config);

        IServiceCollection services = new ServiceCollection();
        ConfigureServices(services, config);

        var sp = services.BuildServiceProvider();

        lock (_lock)
        {
            _customConfigProviders.Add(sp);
        }

        List<object> outputServices = [];
        foreach (Type interfaceType in interfaceTypes)
        {
            outputServices.AddRange(sp.GetServices(interfaceType)!);
        }

        return outputServices;
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment
        {
            EnvironmentName = config["ASPNETCORE_ENVIRONMENT"] ?? "IntegrationTest",
            ApplicationName = "Altinn.Notifications.IntegrationTests",
            ContentRootPath = Directory.GetCurrentDirectory()
        });
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddLogging();

        var sharedDataSource = GetSharedDataSource();
        services.AddSingleton(sharedDataSource);
        RegisterRepositories(services);
        services.AddCoreServices(config);
        services.AddKafkaServices(config);
        services.AddAltinnClients(config);
        services.AddAuthorizationService(config);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
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
