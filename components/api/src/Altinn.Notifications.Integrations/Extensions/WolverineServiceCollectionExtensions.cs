using System.Diagnostics.CodeAnalysis;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Configuration;
using Altinn.Notifications.Shared.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Integrations.Extensions;

/// <summary>
/// Extension methods for registering Wolverine with Azure Service Bus in the Notifications API.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WolverineServiceCollectionExtensions
{
    /// <summary>
    /// Adds Wolverine with Azure Service Bus transport.
    /// Only called when <see cref="WolverineSettingsBase.EnableWolverine"/> is <c>true</c>
    /// (gated in Program.cs). Each listener/publisher queue is individually enabled via its own flag.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="env">The host environment (used for dev/prod ASB emulator detection).</param>
    public static void AddWolverineServices(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        IConfigurationSection wolverineSection = config.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();

        services.Configure<WolverineSettings>(wolverineSection);

        // Set static settings on handlers before Wolverine discovers and configures them.
        EmailDeliveryReportHandler.Settings = wolverineSettings;

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(env, wolverineSettings.ServiceBusConnectionString);
            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());

            // Listeners 
            AddEmailDeliveryReportListener(wolverineSettings, opts);

            // Publishers
            AddSendEmailPublisher(services, wolverineSettings, opts);
            AddSendSmsPublisher(services, wolverineSettings, opts);
        });
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="SendEmailCommand"/>,
    /// routing outbound commands to the Azure Service Bus email send queue.
    /// </summary>
    private static void AddSendEmailPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendEmailPublisher)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
        {
            return;
        }

        // Replace the disabled publisher with the real Wolverine-based publisher
        services.RemoveAll<IEmailCommandPublisher>();
        services.AddSingleton<IEmailCommandPublisher, EmailCommandPublisher>();

        wolverineOptions.PublishMessage<SendEmailCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName);
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="SendSmsCommand"/>,
    /// </summary>
    private static void AddSendSmsPublisher(IServiceCollection services, WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendSmsPublisher)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.SendSmsQueueName))
        {
            return;
        }

        // Replace the disabled publisher with the real Wolverine-based publisher
        services.RemoveAll<ISendSmsPublisher>();
        services.AddSingleton<ISendSmsPublisher, SendSmsCommandPublisher>();

        wolverineOptions.PublishMessage<SendSmsCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.SendSmsQueueName);
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email delivery report queue.
    /// Uses <see cref="EventGridEnvelopeMapper"/> to interop with Event Grid message format.
    /// </summary>
    private static void AddEmailDeliveryReportListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableEmailDeliveryReportListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailDeliveryReportQueueName))
        {
            return;
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailDeliveryReportQueueName)
                        .InteropWith(new EventGridEnvelopeMapper())
                        .ListenerCount(wolverineSettings.ListenerCount);
    }
}
