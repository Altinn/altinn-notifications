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
using Altinn.Notifications.Integrations.InstantEmailService;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Integrations.Kafka.Producers;
using Altinn.Notifications.Integrations.Register;
using Altinn.Notifications.Integrations.SendCondition;
using Altinn.Notifications.Integrations.ShortMessageService;
using Altinn.Notifications.Integrations.Telemetry;
using Altinn.Notifications.Integrations.Wolverine.Publishers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Integrations.Extensions;

/// <summary>
/// Extension class for <see cref="IServiceCollection"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kafka consumer services and configurations to the DI container.
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
            .AddHostedService<SmsStatusConsumer>()
            .AddHostedService<EmailStatusConsumer>()
            .AddHostedService<PastDueOrdersConsumer>()
            .AddHostedService<SmsStatusRetryConsumer>()
            .AddHostedService<EmailStatusRetryConsumer>()
            .AddHostedService<PastDueOrdersRetryConsumer>()
            .AddHostedService<AltinnServiceUpdateConsumer>()
            .Configure<KafkaSettings>(config.GetSection(nameof(KafkaSettings)));
    }

    /// <summary>
    /// Adds ASB/Wolverine-backed notification services to the DI container.
    /// </summary>
    /// <param name="services">service collection.</param>
    /// <param name="config">the configuration collection</param>
    public static void AddNotificationServices(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddSingleton<DeliveryReportMetrics>()
            .AddHostedService<SmsPublishBackgroundService>()
            .AddHostedService<EmailPublishBackgroundService>();

        WolverineSettings wolverineSettings = config.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>() ?? new WolverineSettings();

        RegisterSmsCommandPublisher(services, wolverineSettings);
        RegisterEmailCommandPublisher(services, wolverineSettings);
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
    /// Registers the <see cref="IPastDueOrderPublisher"/> ASB implementation.
    /// </summary>
    private static void RegisterPastDueOrderPublisher(IServiceCollection services, WolverineSettings wolverineSettings)
    {
        if (wolverineSettings.EnableWolverine && wolverineSettings.EnablePastDueOrderPublisher)
        {
            if (!wolverineSettings.EnablePastDueOrderListener)
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.EnablePastDueOrderListener)} must be enabled when {nameof(WolverineSettings.EnablePastDueOrderPublisher)} is enabled.");
            }

            if (string.IsNullOrWhiteSpace(wolverineSettings.PastDueOrdersQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.PastDueOrdersQueueName)} must be configured when {nameof(WolverineSettings.EnablePastDueOrderPublisher)} is enabled.");
            }

            services.AddSingleton<IPastDueOrderPublisher, PastDueOrderPublisher>();
        }
    }

    /// <summary>
    /// Registers the <see cref="IEmailCommandPublisher"/> ASB implementation.
    /// </summary>
    private static void RegisterEmailCommandPublisher(IServiceCollection services, WolverineSettings wolverineSettings)
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
    }

    /// <summary>
    /// Registers the <see cref="ISendSmsPublisher"/> ASB implementation.
    /// </summary>
    private static void RegisterSmsCommandPublisher(IServiceCollection services, WolverineSettings wolverineSettings)
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
    }
}
