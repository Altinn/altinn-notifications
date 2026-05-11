using Altinn.Notifications.Shared.Configuration;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Consumers;
using Altinn.Notifications.Sms.Integrations.LinkMobility;
using Altinn.Notifications.Sms.Integrations.Producers;
using Altinn.Notifications.Sms.Integrations.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        WolverineSettings wolverineSettings = config.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>() ?? new WolverineSettings();

        RegisterSmsDeliveryReportPublisher(services, wolverineSettings, kafkaSettings);
        RegisterSmsSendResultDispatcher(services, wolverineSettings, kafkaSettings);

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
    /// </list>
    /// the <see cref="AsbSmsDeliveryReportPublisher"/> (Azure Service Bus via Wolverine) is registered.
    /// Otherwise, the Kafka-based <see cref="KafkaSmsDeliveryReportPublisher"/> is registered.
    /// This matches the guard used in <see cref="Extensions.WolverineServiceCollectionExtensions"/> so that
    /// exactly one fully-configured transport path is active at a time.
    /// </remarks>
    private static void RegisterSmsDeliveryReportPublisher(IServiceCollection services, WolverineSettings wolverineSettings, KafkaSettings kafkaSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableSmsDeliveryReportPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.SmsDeliveryReportQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.SmsDeliveryReportQueueName)} must be configured when {nameof(WolverineSettings.EnableSmsDeliveryReportPublisher)} is enabled.");
            }

            services.AddSingleton<ISmsDeliveryReportPublisher, AsbSmsDeliveryReportPublisher>();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(kafkaSettings.SmsStatusUpdatedTopicName))
            {
                throw new InvalidOperationException(
                    $"{nameof(KafkaSettings.SmsStatusUpdatedTopicName)} must be configured when the Wolverine SMS delivery report publisher is disabled.");
            }

            services.AddSingleton<ISmsDeliveryReportPublisher, KafkaSmsDeliveryReportPublisher>();
        }
    }

    /// <summary>
    /// Registers the appropriate <see cref="ISmsSendResultDispatcher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterSmsSendResultDispatcher(IServiceCollection services, WolverineSettings wolverineSettings, KafkaSettings kafkaSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableSmsSendResultPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.SmsSendResultQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.SmsSendResultQueueName)} must be configured when {nameof(WolverineSettings.EnableSmsSendResultPublisher)} is enabled.");
            }

            services.AddSingleton<ISmsSendResultDispatcher, SmsSendResultPublisher>();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(kafkaSettings.SmsStatusUpdatedTopicName))
            {
                throw new InvalidOperationException(
                    $"{nameof(KafkaSettings.SmsStatusUpdatedTopicName)} must be configured when the Wolverine SMS send result publisher is disabled.");
            }

            services.AddSingleton<ISmsSendResultDispatcher, SmsSendResultProducer>();
        }
    }
}
