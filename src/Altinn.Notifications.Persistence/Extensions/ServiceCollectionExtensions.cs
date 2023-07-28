using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Repository;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        PostgreSqlSettings? settings = config.GetSection("PostgreSQLSettings").Get<PostgreSqlSettings>();

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required PostgreSQLSettings is missing from application configuration");
        }

        string connectionString = string.Format(settings.ConnectionString, settings.NotificationsDbPwd);

        return services
        .AddSingleton<IOrderRepository, OrderRepository>()
        .AddSingleton<IEmailNotificationRepository, EmailNotificationRepository>()
        .AddNpgsqlDataSource(connectionString, builder => builder.EnableParameterLogging(settings.LogParameters));
    }
}