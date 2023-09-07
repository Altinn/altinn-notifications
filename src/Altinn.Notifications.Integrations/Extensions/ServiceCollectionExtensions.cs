using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Health;
using Altinn.Notifications.Integrations.Kafka.Consumers;
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
    /// Adds kafka services and configurations to DI container.
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddKafkaServices(this IServiceCollection services, IConfiguration config)
    {
        KafkaSettings? kafkaSettings = config.GetSection(nameof(KafkaSettings)).Get<KafkaSettings>();

        if (kafkaSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required KafkaSettings is missing from application configuration");
        }

        services
        .AddSingleton<IKafkaProducer, KafkaProducer>()
        .AddHostedService<PastDueOrdersConsumer>()
        .AddHostedService<PastDueOrdersRetryConsumer>()
        .AddHostedService<EmailStatusConsumer>()
        .Configure<KafkaSettings>(config.GetSection(nameof(KafkaSettings)));
    }

    /// <summary>
    /// Adds kafka health checks
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddKafkaHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        KafkaSettings kafkaSettings = config!.GetSection(nameof(KafkaSettings)).Get<KafkaSettings>()!;

        if (kafkaSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required KafkaSettings is missing from application configuration");
        }

        services.AddHealthChecks()
        .AddCheck("notifications_kafka_health_check", new KafkaHealthCheck(kafkaSettings));
    }
}