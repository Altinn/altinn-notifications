using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Health;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Altinn.Notifications.Persistence.Extensions;

/// <summary>
/// Extension class for <see cref="IServiceCollection"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds persistence services to DI container.
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static IServiceCollection AddPostgresRepositories(this IServiceCollection services, IConfiguration config)
    {
        PostgreSqlSettings? settings = config.GetSection("PostgreSQLSettings")
            .Get<PostgreSqlSettings>()
            ?? throw new ArgumentNullException(nameof(config), "Required PostgreSQLSettings is missing from application configuration");

        string connectionString = string.Format(settings.ConnectionString, settings.NotificationsDbPwd);

        return services
        .AddSingleton<IOrderRepository, OrderRepository>()
        .AddSingleton<IEmailNotificationRepository, EmailNotificationRepository>()
        .AddSingleton<INotificationSummaryRepository, NotificationSummaryRepository>()
        .AddSingleton<IResourceLimitRepository, ResourceLimitRepository>()
        .AddNpgsqlDataSource(connectionString, builder =>
            builder.EnableParameterLogging(settings.LogParameters)
                   .EnableDynamicJson());
    }

    /// <summary>
    /// Adds postgresql health checks
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddPostgresHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("notifications_postgres_health_check");
    }
}
