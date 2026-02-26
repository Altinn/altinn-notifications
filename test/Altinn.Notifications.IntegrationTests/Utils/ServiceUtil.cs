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
    private static ServiceProvider? _sharedServiceProvider;

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
            _sharedServiceProvider?.Dispose();
            _sharedServiceProvider = null;
            _sharedDataSource?.Dispose();
            _sharedDataSource = null;
        }
    }

    public static List<object> GetServices(List<Type> interfaceTypes, Dictionary<string, string>? envVariables = null)
    {
        // When env variables are customized (e.g., Kafka topic names), build a one-off provider
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
            var outputServiceObject = _sharedServiceProvider.GetServices(interfaceType)!;
            outputServices.AddRange(outputServiceObject!);
        }

        return outputServices;
    }

    private static ServiceProvider BuildSharedServiceProvider()
    {
        var config = BuildConfiguration();

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection();
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

        return services.BuildServiceProvider();
    }

    private static List<object> BuildServiceProvider(Dictionary<string, string> envVariables, List<Type> interfaceTypes)
    {
        var config = BuildConfiguration(envVariables);

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection();
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

        var sp = services.BuildServiceProvider();
        List<object> outputServices = [];
        foreach (Type interfaceType in interfaceTypes)
        {
            outputServices.AddRange(sp.GetServices(interfaceType)!);
        }

        return outputServices;
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
