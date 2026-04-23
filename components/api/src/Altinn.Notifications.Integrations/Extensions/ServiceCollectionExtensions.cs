using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Common.AccessTokenClient.Services;
using Altinn.Common.PEP.Clients;
using Altinn.Common.PEP.Implementation;
using Altinn.Common.PEP.Interfaces;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Integrations.Authorization;
using Altinn.Notifications.Integrations.Clients;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Health;
using Altinn.Notifications.Integrations.InstantEmailService;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Integrations.Kafka.Producers;
using Altinn.Notifications.Integrations.Kafka.Publishers;
using Altinn.Notifications.Integrations.Register;
using Altinn.Notifications.Integrations.SendCondition;
using Altinn.Notifications.Integrations.ShortMessageService;
using Altinn.Notifications.Integrations.Wolverine.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        KafkaSettings kafkaSettings = config.GetSection(nameof(KafkaSettings))
            .Get<KafkaSettings>()
            ?? throw new ArgumentNullException(nameof(config), "Required KafkaSettings is missing from application configuration");

        services
        .AddSingleton<IKafkaProducer, KafkaProducer>()
        .AddHostedService<SmsStatusConsumer>()
        .AddHostedService<EmailStatusConsumer>()
        .AddHostedService<PastDueOrdersConsumer>()
        .AddHostedService<SmsStatusRetryConsumer>()
        .AddHostedService<EmailStatusRetryConsumer>()
        .AddHostedService<PastDueOrdersRetryConsumer>()
        .AddHostedService<AltinnServiceUpdateConsumer>()
        .AddHostedService<SmsPublishBackgroundService>()
        .AddHostedService<EmailPublishBackgroundService>()
        .Configure<KafkaSettings>(config.GetSection(nameof(KafkaSettings)));

        WolverineSettings wolverineSettings = config.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>() ?? new WolverineSettings();

        RegisterSmsCommandPublisher(services, wolverineSettings, kafkaSettings);
        RegisterEmailCommandPublisher(services, wolverineSettings, kafkaSettings);
        RegisterPastDueOrderPublisher(services, wolverineSettings);
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
        services.AddHttpClient<IShortMessageServiceClient, ShortMessageServiceClient>();
        services.AddHttpClient<IInstantEmailServiceClient, InstantEmailServiceClient>();
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
        services.AddTransient<IAccessTokenGenerator, AccessTokenGenerator>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddTransient<ISigningCredentialsResolver, SigningCredentialsResolver>();

        services.AddMaskinportenHttpClient<SettingsJwkClientDefinition, IConditionClient, SendConditionClient>(
            config.GetSection("SendConditionClient:MaskinportenSettings"));
    }

    /// <summary>
    /// Adds Kafka health checks
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

    /// <summary>
    /// Registers the appropriate <see cref="IPastDueOrderPublisher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterPastDueOrderPublisher(IServiceCollection services, WolverineSettings wolverineSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnablePastDueOrderPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.PastDueOrdersQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.PastDueOrdersQueueName)} must be configured when {nameof(WolverineSettings.EnablePastDueOrderPublisher)} is enabled.");
            }

            services.AddSingleton<IPastDueOrderPublisher>(sp =>
                new PastDueOrderPublisher(
                    sp.GetRequiredService<ILogger<PastDueOrderPublisher>>(),
                    sp));
        }
        else
        {
            services.AddSingleton<IPastDueOrderPublisher, KafkaPastDueOrderPublisher>();
        }
    }

    /// <summary>
    /// Registers the appropriate <see cref="IEmailCommandPublisher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterEmailCommandPublisher(IServiceCollection services, WolverineSettings wolverineSettings, KafkaSettings kafkaSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableSendEmailPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EmailSendQueueName)} must be configured when {nameof(WolverineSettings.EnableSendEmailPublisher)} is enabled.");
            }

            services.AddSingleton<IEmailCommandPublisher, EmailCommandPublisher>();
        }
        else
        {
            services.AddSingleton<IEmailCommandPublisher>(sp =>
                new KafkaEmailCommandPublisher(
                    sp.GetRequiredService<IKafkaProducer>(),
                    kafkaSettings.EmailQueueTopicName));
        }
    }

    /// <summary>
    /// Registers the appropriate <see cref="ISendSmsPublisher"/> implementation
    /// based on Wolverine configuration, selecting either the ASB or Kafka transport path.
    /// </summary>
    private static void RegisterSmsCommandPublisher(IServiceCollection services, WolverineSettings wolverineSettings, KafkaSettings kafkaSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnableSendSmsPublisher)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.SendSmsQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.SendSmsQueueName)} must be configured when {nameof(WolverineSettings.EnableSendSmsPublisher)} is enabled.");
            }

            services.AddSingleton<ISendSmsPublisher, SendSmsCommandPublisher>();
        }
        else
        {
            services.AddSingleton<ISendSmsPublisher>(sp =>
                new KafkaSendSmsPublisher(
                    sp.GetRequiredService<IKafkaProducer>(),
                    kafkaSettings.SmsQueueTopicName));
        }
    }
}
