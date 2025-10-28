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

    public static void DisposeSharedDataSource()
    {
        lock (_lock)
        {
            _sharedDataSource?.Dispose();
            _sharedDataSource = null;
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

        // Register all repository implementations using the shared data source
        RegisterRepositories(services);

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

    private static void RegisterRepositories(IServiceCollection services)
    {
        // Get all repository interface types from Core.Persistence
        var coreAssembly = typeof(IOrderRepository).Assembly;
        var repositoryInterfaces = coreAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace == "Altinn.Notifications.Core.Persistence");

        // Get all implementation types from the Persistence assembly
        var persistenceAssembly = typeof(OrderRepository).Assembly;
        var implementationTypes = persistenceAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract);

        // Register each interface with its implementation
        foreach (var interfaceType in repositoryInterfaces)
        {
            var implementationType = implementationTypes
                .FirstOrDefault(t => interfaceType.IsAssignableFrom(t));

            if (implementationType != null)
            {
                services.AddSingleton(interfaceType, implementationType);
            }
        }
    }
}
