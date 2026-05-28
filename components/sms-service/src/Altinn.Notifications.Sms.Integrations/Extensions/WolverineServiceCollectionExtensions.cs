using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Extensions;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Publishers;
using Altinn.Notifications.Sms.Integrations.Wolverine.Policies;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Sms.Integrations.Extensions;

/// <summary>
/// Extension methods for registering Wolverine with Azure Service Bus in the SMS service.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineServiceCollectionExtensions
{
    /// <summary>
    /// Adds Wolverine with Azure Service Bus transport.
    /// Publisher queues are mandatory. Listener queues are individually enabled via their own flags.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="hostEnvironment">The host environment (used for dev/prod ASB emulator detection).</param>
    public static void AddWolverineServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        IConfigurationSection wolverineSection = configuration.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();
        if (!wolverineSettings.EnableWolverine)
        {
            return;
        }

        services
            .AddSingleton(wolverineSettings)
            .Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(hostEnvironment, wolverineSettings.ServiceBusConnectionString);
            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());

            // Listeners
            AddSendSmsListener(wolverineSettings, opts);

            // Publishers
            AddSmsSendResultPublisher(services, wolverineSettings, opts);
            AddSmsDeliveryReportPublisher(services, wolverineSettings, opts);
        });
    }

    private static void AddSendSmsListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendSmsListener)
        {
            return;
        }

        if (wolverineSettings.SendSmsListenerCount <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SendSmsListenerCount)} must be greater than 0 when {nameof(WolverineSettings.EnableSendSmsListener)} is enabled.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SendSmsQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SendSmsQueueName)} must be configured when {nameof(WolverineSettings.EnableSendSmsListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.SendSmsQueueName)
                        .ListenerCount(wolverineSettings.SendSmsListenerCount);

        wolverineOptions.Policies.Add(new SendSmsCommandHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Configures Wolverine to publish <see cref="SmsDeliveryReportCommand"/> messages
    /// to the Azure Service Bus SMS delivery report queue and registers
    /// <see cref="SmsDeliveryReportPublisher"/> as the <see cref="ISmsDeliveryReportPublisher"/> implementation.
    /// </summary>
    private static void AddSmsDeliveryReportPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSmsDeliveryReportPublisher)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EnableSmsDeliveryReportPublisher)} cannot be disabled — there is no alternative publisher implementation.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SmsDeliveryReportQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsDeliveryReportQueueName)} must be configured when {nameof(WolverineSettings.EnableSmsDeliveryReportPublisher)} is enabled.");
        }

        services.AddSingleton<ISmsDeliveryReportPublisher, SmsDeliveryReportPublisher>();

        wolverineOptions.PublishMessage<SmsDeliveryReportCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.SmsDeliveryReportQueueName);
    }

    /// <summary>
    /// Configures Wolverine to publish <see cref="SmsSendResultCommand"/> messages
    /// to the Azure Service Bus SMS send result queue and registers
    /// <see cref="SmsSendResultPublisher"/> as the <see cref="ISmsSendResultDispatcher"/> implementation.
    /// </summary>
    private static void AddSmsSendResultPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSmsSendResultPublisher)
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EnableSmsSendResultPublisher)} cannot be disabled — there is no alternative publisher implementation.");
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SmsSendResultQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsSendResultQueueName)} must be configured when {nameof(WolverineSettings.EnableSmsSendResultPublisher)} is enabled.");
        }

        services.AddSingleton<ISmsSendResultDispatcher, SmsSendResultPublisher>();

        wolverineOptions.PublishMessage<SmsSendResultCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.SmsSendResultQueueName);
    }
}
