using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Integrations.Clients;
using Altinn.Notifications.Email.Integrations.Consumers;
using Altinn.Notifications.Email.Integrations.Health;
using Altinn.Notifications.Email.Integrations.Producers;
using Altinn.Notifications.Email.Integrations.Publishers;

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
            .AddSingleton<ICommonProducer, CommonProducer>()
            .AddHostedService<EmailSendingAcceptedConsumer>()
            .AddSingleton<IEmailServiceClient, EmailServiceClient>()
            .AddSingleton(kafkaSettings)
            .AddSingleton(emailServiceAdminSettings)
            .AddSingleton(communicationServicesSettings);

        WolverineSettings wolverineSettings = config.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>() ?? new WolverineSettings();

        RegisterEmailSendResultDispatcher(services, wolverineSettings, kafkaSettings);
        RegisterEmailStatusCheckDispatcher(services, wolverineSettings, kafkaSettings);

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
    private static void RegisterEmailStatusCheckDispatcher(IServiceCollection services, WolverineSettings wolverineSettings, KafkaSettings kafkaSettings)
    {
        services.RemoveAll<IEmailStatusCheckDispatcher>();

        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableEmailStatusCheckListener)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusCheckQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EmailStatusCheckQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailStatusCheckListener)} is enabled.");
            }

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

    /// <summary>
    /// Registers the appropriate <see cref="IEmailSendResultDispatcher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterEmailSendResultDispatcher(IServiceCollection services, WolverineSettings wolverineSettings, KafkaSettings kafkaSettings)
    {
        services.RemoveAll<IEmailSendResultDispatcher>();

        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableEmailSendResultPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendResultQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EmailSendResultQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailSendResultPublisher)} is enabled.");
            }

            services.AddSingleton<IEmailSendResultDispatcher, EmailSendResultPublisher>();
        }
        else
        {
            services.AddSingleton<IEmailSendResultDispatcher>(sp =>
                new EmailSendResultProducer(
                    sp.GetRequiredService<ICommonProducer>(),
                    kafkaSettings.EmailStatusUpdatedTopicName));
        }
    }
}
