using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Clients;
using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Health;
using Altinn.Notifications.Email.Integrations.Producers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// This class is responsible for holding extension methods for program startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add necessary integration services and configuration to the service collection.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The given service collection.</returns>
    public static IServiceCollection AddIntegrationServices(this IServiceCollection services, IConfiguration config)
    {
        KafkaSettings kafkaSettings = config!.GetSection(nameof(KafkaSettings)).Get<KafkaSettings>()!;

        if (kafkaSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required Kafka settings is missing from application configuration");
        }

        CommunicationServicesSettings communicationServicesSettings = config!.GetSection(nameof(CommunicationServicesSettings)).Get<CommunicationServicesSettings>()!;

        if (communicationServicesSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required communication services settings is missing from application configuration");
        }

        services
            .AddSingleton<IEmailServiceClient, EmailServiceClient>()
            .AddSingleton<ICommonProducer, CommonProducer>()
            .AddHostedService<SendEmailQueueConsumer>()
            .AddHostedService<EmailSendingAcceptedConsumer>()
            .AddSingleton(kafkaSettings)
            .AddSingleton(communicationServicesSettings);
        return services;
    }

    /// <summary>
    /// Adds health checks for integrations 
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddIntegrationHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        KafkaSettings kafkaSettings = config!.GetSection(nameof(KafkaSettings)).Get<KafkaSettings>()!;

        if (kafkaSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required Kafka settings is missing from application configuration");
        }

        services.AddHealthChecks()
        .AddCheck("notifications_kafka_health_check", new KafkaHealthCheck(kafkaSettings));
    }
}
