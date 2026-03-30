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

            // Listeners (ASB queues will be auto-provisioned in production)
            if (wolverineSettings.EnableEmailDeliveryReportListener && !string.IsNullOrWhiteSpace(wolverineSettings.EmailDeliveryReportQueueName))
            {
                opts.ListenToAzureServiceBusQueue(wolverineSettings.EmailDeliveryReportQueueName)
                    .InteropWith(new EventGridEnvelopeMapper())
                    .ListenerCount(wolverineSettings.ListenerCount);
            }

            // Publishers
            if (!string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
            {
                opts.PublishMessage<SendEmailCommand>()
                    .ToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName);
            }
            if (!string.IsNullOrWhiteSpace(wolverineSettings.SendSmsQueueName))
            {
                opts.PublishMessage<SendSmsCommand>()
                .ToAzureServiceBusQueue(wolverineSettings.SendSmsQueueName);

                // Replace the disabled publisher with the real Wolverine-based publisher
                services.RemoveAll<ISendSmsPublisher>();
                services.AddSingleton<ISendSmsPublisher, SendSmsPublisher>();
            }
        });

        // Replace the disabled publisher with the real Wolverine-based publisher
        services.RemoveAll<IEmailCommandPublisher>();
        services.AddSingleton<IEmailCommandPublisher, EmailCommandPublisher>();
    }
}
