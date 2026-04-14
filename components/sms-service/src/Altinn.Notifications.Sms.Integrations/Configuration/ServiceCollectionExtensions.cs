using Altinn.Notifications.Shared.Configuration;
using Altinn.Notifications.Sms.Core.Configuration;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Consumers;
using Altinn.Notifications.Sms.Integrations.LinkMobility;
using Altinn.Notifications.Sms.Integrations.Producers;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

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

        SmsGatewaySettings smsGatewaySettings = config!.GetSection(nameof(SmsGatewaySettings)).Get<SmsGatewaySettings>()!;

        if (smsGatewaySettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required SmsGatewayConfiguration settings is missing from application configuration.");
        }

        services
            .AddSingleton<ICommonProducer, CommonProducer>()
            .AddHostedService<SendSmsQueueConsumer>()
            .AddSingleton(kafkaSettings)
            .AddSingleton<ISmsClient, SmsClient>()
            .AddSingleton<IAltinnGatewayClient, AltinnGatewayClient>()
            .AddSingleton(smsGatewaySettings);

        RegisterSmsDeliveryReportPublisher(services, config);

        return services;
    }

    /// <summary>
    /// Registers the appropriate <see cref="ISmsDeliveryReportPublisher"/> implementation based on
    /// the configured Wolverine SMS delivery report settings.
    /// </summary>
    /// <remarks>
    /// When all of the following conditions are met:
    /// <list type="bullet">
    ///   <item><description><see cref="WolverineSettingsBase.EnableWolverine"/> is <c>true</c></description></item>
    ///   <item><description><see cref="WolverineSettings.EnableSmsDeliveryReportPublisher"/> is <c>true</c></description></item>
    ///   <item><description><see cref="WolverineSettings.SmsDeliveryReportQueueName"/> is non-empty</description></item>
    /// </list>
    /// the <see cref="AsbSmsDeliveryReportPublisher"/> (Azure Service Bus via Wolverine) is registered.
    /// Otherwise, the Kafka-based <see cref="KafkaSmsDeliveryReportPublisher"/> is registered.
    /// This matches the guard used in <see cref="Extensions.WolverineServiceCollectionExtensions"/> so that
    /// exactly one fully-configured transport path is active at a time.
    /// </remarks>
    private static void RegisterSmsDeliveryReportPublisher(IServiceCollection services, IConfiguration config)
    {
        IConfigurationSection wolverineSection = config.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();

        bool useWolverine =
            wolverineSettings.EnableWolverine &&
            wolverineSettings.EnableSmsDeliveryReportPublisher &&
            !string.IsNullOrWhiteSpace(wolverineSettings.SmsDeliveryReportQueueName);

        services.RemoveAll<ISmsDeliveryReportPublisher>();

        if (useWolverine)
        {
            services.AddSingleton<ISmsDeliveryReportPublisher>(sp =>
                new AsbSmsDeliveryReportPublisher(sp));
        }
        else
        {
            services.AddSingleton<ISmsDeliveryReportPublisher>(sp =>
                new KafkaSmsDeliveryReportPublisher(
                    sp.GetRequiredService<ICommonProducer>(),
                    sp.GetRequiredService<TopicSettings>().SmsStatusUpdatedTopicName));
        }
    }
}
