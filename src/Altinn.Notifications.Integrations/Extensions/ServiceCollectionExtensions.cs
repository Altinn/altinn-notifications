using Altinn.Common.PEP.Clients;
using Altinn.Common.PEP.Implementation;
using Altinn.Common.PEP.Interfaces;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Authorization;
using Altinn.Notifications.Integrations.Clients;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Health;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Integrations.Kafka.Producers;
using Altinn.Notifications.Integrations.Register;

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
            ?? throw new ArgumentNullException(nameof(config), "Required PlatformSettings is missing from application configuration");

        services.Configure<PlatformSettings>(config.GetSection(nameof(PlatformSettings)));
        services.AddHttpClient<IProfileClient, ProfileClient>();
        services.AddHttpClient<IRegisterClient, RegisterClient>();
    }

    /// <summary>
    /// Adds services and other dependencies used for authorization.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The configuration collection</param>
    public static void AddAuthorizationService(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<Common.PEP.Configuration.PlatformSettings>(config.GetSection("PlatformSettings"));
        services.AddHttpClient<AuthorizationApiClient>();
        services.AddSingleton<IPDP, PDPAppSI>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
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
