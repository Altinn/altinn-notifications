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

using Npgsql;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class ServiceUtil
{
    private static readonly object _lock = new();
    private static NpgsqlDataSource? _sharedDataSource;

    private static NpgsqlDataSource GetOrCreateDataSource(IConfiguration config)
    {
        if (_sharedDataSource != null)
        {
            return _sharedDataSource;
        }

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

            _sharedDataSource = dataSourceBuilder.Build();

            return _sharedDataSource;
        }
    }

    public static List<object> GetServices(List<Type> interfaceTypes, Dictionary<string, string>? envVariables = null)
    {
        if (envVariables != null)
        {
            foreach (var item in envVariables)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }

        var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json")
            .AddEnvironmentVariables();

        var config = builder.Build();

        WebApplication.CreateBuilder()
                       .Build()
                       .SetUpPostgreSql(true, config);

        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddLogging();

        // Register the shared data source as a singleton
        var sharedDataSource = GetOrCreateDataSource(config);
        services.AddSingleton(sharedDataSource);

        // Register repositories that will use the shared data source
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<IMetricsRepository, MetricsRepository>();
        services.AddSingleton<IStatusFeedRepository, StatusFeedRepository>();
        services.AddSingleton<IResourceLimitRepository, ResourceLimitRepository>();
        services.AddSingleton<ISmsNotificationRepository, SmsNotificationRepository>();
        services.AddSingleton<IEmailNotificationRepository, EmailNotificationRepository>();
        services.AddSingleton<INotificationSummaryRepository, NotificationSummaryRepository>();
        services.AddSingleton<INotificationDeliveryManifestRepository, NotificationDeliveryManifestRepository>();
        services.AddSingleton<IDeadDeliveryReportRepository, DeadDeliveryReportRepository>();

        services.AddCoreServices(config);
        services.AddKafkaServices(config);
        services.AddAltinnClients(config);
        services.AddAuthorizationService(config);

        var serviceProvider = services.BuildServiceProvider();
        List<object> outputServices = new();

        foreach (Type interfaceType in interfaceTypes)
        {
            var outputServiceObject = serviceProvider.GetServices(interfaceType)!;
            outputServices.AddRange(outputServiceObject!);
        }

        return outputServices;
    }
}
