using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Configuration;
using Altinn.Notifications.Persistence.Repository;

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
    /// <param name="settings">Postgresql settings collection</param>
    public static IServiceCollection AddPostgresRepositories(this IServiceCollection services, PostgreSqlSettings settings)
    {
        string connectionString = string.Format(settings.ConnectionString, settings.NotificationsDbPwd);

        return services
        .AddSingleton<IOrderRepository, OrderRepository>()
        .AddNpgsqlDataSource(connectionString, builder => builder.EnableParameterLogging(settings.LogParameters));
    }
}