using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Integrations.Wolverine.Policies;
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
    /// <param name="configuration">The application configuration.</param>
    /// <param name="hostEnvironment">The host environment (used for dev/prod ASB emulator detection).</param>
    public static void AddWolverineServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        IConfigurationSection wolverineSection = configuration.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();

        services.Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(hostEnvironment, wolverineSettings.ServiceBusConnectionString);

            opts.Policies.AllSenders(x => x.SendInline());
            opts.Policies.AllListeners(x => x.ProcessInline());

            // Listeners
            AddEmailSendResultListener(wolverineSettings, opts);
            AddEmailDeliveryReportListener(wolverineSettings, opts);

            // Publishers
            AddSendEmailPublisher(wolverineSettings, opts);
        });

        // Replace the disabled publisher with the real Wolverine-based publisher
        services.RemoveAll<IEmailCommandPublisher>();
        services.AddSingleton<IEmailCommandPublisher, EmailCommandPublisher>();
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email delivery report queue,
    /// enabling the API to consume Event Grid–forwarded delivery reports.
    /// This method is invoked only when <see cref="WolverineSettings.EnableEmailDeliveryReportListener"/> is <c>true</c>.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
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

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email send result queue,
    /// enabling the API to consume <see cref="EmailSendResultCommand"/> messages
    /// published by the email service.
    /// This method is invoked only when <see cref="WolverineSettings.EnableEmailSendResultListener"/> is <c>true</c>.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailSendResultListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableEmailSendResultListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendResultQueueName))
        {
            return;
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailSendResultQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new EmailSendResultHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="SendEmailCommand"/>, routing
    /// outbound commands to the Azure Service Bus email send queue consumed by the email service.
    /// This method is invoked only when <see cref="WolverineSettings.EnableSendEmailPublisher"/> is <c>true</c>.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddSendEmailPublisher(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendEmailPublisher)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
        {
            return;
        }

        wolverineOptions.PublishMessage<SendEmailCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName);
    }
}
