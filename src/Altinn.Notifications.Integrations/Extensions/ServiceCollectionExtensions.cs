using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Kafka.Producers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Integrations.Extensions;

/// <summary>
/// Extension class for <see cref="IServiceCollection"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core services and configurations to DI container.
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static IServiceCollection AddKafkaServices(this IServiceCollection services, IConfiguration config)
    {
        return services
              .AddSingleton<IKafkaProducer, KafkaProducer>()
              .Configure<KafkaSettings>(config.GetSection("KafkaSettings"));
    }
}