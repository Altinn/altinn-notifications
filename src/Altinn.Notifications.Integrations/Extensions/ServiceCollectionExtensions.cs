using Altinn.Notifications.Core.Integrations.Consumers;
using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Health;
using Altinn.Notifications.Integrations.Kafka.Producers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Altinn.Notifications.Integrations.Extensions;

/// <summary>
/// Extension class for <see cref="IServiceCollection"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds kafka services and configurations to DI container.
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddKafkaServices(this IServiceCollection services, IConfiguration config)
    {
        services
        .AddSingleton<IKafkaProducer, KafkaProducer>()
        .AddSingleton<IHostedService, PastDueOrdersConsumer>()
               .Configure<KafkaSettings>(config.GetSection("KafkaSettings"));
    }

    /// <summary>
    /// Adds kafka health checks
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static IServiceCollection AddKafkaHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        KafkaSettings kafkaSettings = config!.GetSection("KafkaSettings").Get<KafkaSettings>()!;

        services.AddHealthChecks()
        .AddCheck("notifications_kafka_health_check", new KafkaHealthCheck(kafkaSettings.BrokerAddress, kafkaSettings.HealthCheckTopic, kafkaSettings.ConsumerGroupId));

        return services;
    }
}