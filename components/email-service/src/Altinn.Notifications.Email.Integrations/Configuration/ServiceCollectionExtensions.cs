using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Clients;
using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Health;
using Altinn.Notifications.Email.Integrations.Producers;
using Altinn.Notifications.Shared.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        EmailServiceAdminSettings emailServiceAdminSettings = config!.GetSection(nameof(EmailServiceAdminSettings)).Get<EmailServiceAdminSettings>()!;

        if (emailServiceAdminSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required email service admin settings is missing from application configuration");
        }

        services
            .AddHostedService<SendEmailQueueConsumer>()
            .AddHostedService<EmailSendingAcceptedConsumer>()
            .AddSingleton<ICommonProducer, CommonProducer>()
            .AddSingleton<IEmailServiceClient, EmailServiceClient>()
            .AddSingleton(kafkaSettings)
            .AddSingleton(emailServiceAdminSettings)
            .AddSingleton(communicationServicesSettings);

        RegisterEmailStatusCheckDispatcher(services, config, kafkaSettings);

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

    /// <summary>
    /// Registers the appropriate <see cref="IEmailStatusCheckDispatcher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterEmailStatusCheckDispatcher(IServiceCollection services, IConfiguration configuration, KafkaSettings kafkaSettings)
    {
        IConfigurationSection wolverineSection = configuration.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();

        bool useWolverine =
             wolverineSettings.EnableWolverine &&
             wolverineSettings.EnableEmailStatusCheckListener &&
             !string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusCheckQueueName);

        services.RemoveAll<IEmailStatusCheckDispatcher>();

        if (useWolverine)
        {
            services.AddSingleton<IEmailStatusCheckDispatcher, EmailStatusCheckPublisher>();
        }
        else
        {
            services.AddSingleton<IEmailStatusCheckDispatcher>(sp =>
                new EmailStatusCheckProducer(
                    sp.GetRequiredService<ICommonProducer>(),
                    sp.GetRequiredService<IDateTimeService>(),
                    kafkaSettings.EmailSendingAcceptedTopicName));
        }
    }
}
