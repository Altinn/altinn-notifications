﻿using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Clients;
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
        _ = config.GetSection(nameof(KafkaSettings))
            .Get<KafkaSettings>()
            ?? throw new ArgumentNullException(nameof(config), "Required KafkaSettings is missing from application configuration");

        services
        .AddSingleton<IKafkaProducer, KafkaProducer>()
        .AddHostedService<PastDueOrdersConsumer>()
        .AddHostedService<PastDueOrdersRetryConsumer>()
        .AddHostedService<EmailStatusConsumer>()
        .AddHostedService<SmsStatusConsumer>()
        .AddHostedService<AltinnServiceUpdateConsumer>()
        .Configure<KafkaSettings>(config.GetSection(nameof(KafkaSettings)));
    }

    /// <summary>
    /// Adds Altinn clients and configurations to DI container.
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddAltinnClients(this IServiceCollection services, IConfiguration config)
    {
        _ = config.GetSection(nameof(PlatformSettings))
            .Get<PlatformSettings>()
            ?? throw new ArgumentNullException(nameof(config), "Required AltinnServiceSettings is missing from application configuration");

        services
            .Configure<PlatformSettings>(config.GetSection(nameof(PlatformSettings)))
            .AddHttpClient<IProfileClient, ProfileClient>();
    }

    /// <summary>
    /// Adds kafka health checks
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddKafkaHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        KafkaSettings kafkaSettings = config!
            .GetSection(nameof(KafkaSettings))
            .Get<KafkaSettings>()
            ?? throw new ArgumentNullException(nameof(config), "Required KafkaSettings is missing from application configuration");

        services.AddHealthChecks()
        .AddCheck("notifications_kafka_health_check", new KafkaHealthCheck(kafkaSettings));
    }
}
