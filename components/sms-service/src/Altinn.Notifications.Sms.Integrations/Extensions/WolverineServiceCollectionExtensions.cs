using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Extensions;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Wolverine.Policies;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.AzureServiceBus;

using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Altinn.Notifications.Sms.Integrations.Extensions;

/// <summary>
/// Extension methods for registering Wolverine with Azure Service Bus in the SMS service.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineServiceCollectionExtensions
{
    /// <summary>
    /// Adds Wolverine with Azure Service Bus transport.
    /// Each listener/publisher queue is individually enabled via its own flag.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="hostEnvironment">The host environment (used for dev/prod ASB emulator detection).</param>
    public static void AddWolverineServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        IConfigurationSection wolverineSection = configuration.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();
        if (!wolverineSettings.EnableWolverine)
        {
            return;
        }

        if (!wolverineSettings.EnableWolverine)
        {
            return;
        }

        services.Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(hostEnvironment, wolverineSettings.ServiceBusConnectionString);
            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());

            // Publishers
            AddSmsDeliveryReportPublisher(wolverineSettings, opts);

            // Listeners: 
            AddSendSmsListener(opts, wolverineSettings);
        });
    }

    private static void AddSendSmsListener(WolverineOptions opts, WolverineSettings wolverineSettings)
    {
        if (wolverineSettings.EnableSendSmsListener)
        {
            if (string.IsNullOrWhiteSpace(wolverineSettings.SendSmsQueueName))
            {
                throw new InvalidOperationException(
                    $"{nameof(WolverineSettings.SendSmsQueueName)} must be set when {nameof(WolverineSettings.EnableSendSmsListener)} is enabled.");
            }

            opts.ListenToAzureServiceBusQueue(wolverineSettings.SendSmsQueueName)
                .ListenerCount(wolverineSettings.ListenerCount);

            opts.Policies.Add(new SendSmsCommandHandlerPolicy(wolverineSettings));
        }
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="SmsDeliveryReportCommand"/>,
    /// routing outbound commands to the Azure Service Bus SMS delivery report queue.
    /// </summary>
    private static void AddSmsDeliveryReportPublisher(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSmsDeliveryReportPublisher)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SmsDeliveryReportQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.SmsDeliveryReportQueueName)} must be configured when {nameof(WolverineSettings.EnableSmsDeliveryReportPublisher)} is enabled.");
        }

        wolverineOptions.PublishMessage<SmsDeliveryReportCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.SmsDeliveryReportQueueName);
    }
}
