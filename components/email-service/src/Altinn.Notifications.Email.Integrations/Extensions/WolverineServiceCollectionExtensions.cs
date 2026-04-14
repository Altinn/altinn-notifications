using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Wolverine.Policies;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.AzureServiceBus;

namespace Altinn.Notifications.Email.Integrations.Extensions;

/// <summary>
/// Extension methods for registering Wolverine with Azure Service Bus in the email service.
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
    public static void AddWolverineServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        IConfigurationSection wolverineSection = configuration.GetSection(nameof(WolverineSettings));
        WolverineSettings wolverineSettings = wolverineSection.Get<WolverineSettings>() ?? new WolverineSettings();
        if (!wolverineSettings.EnableWolverine)
        {
            return;
        }

        services.Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(hostEnvironment, wolverineSettings.ServiceBusConnectionString);

            opts.Policies.AllSenders(x => x.SendInline());
            opts.Policies.AllListeners(x => x.ProcessInline());

            // Listeners
            AddEmailSendQueueListener(wolverineSettings, opts);
            AddEmailStatusCheckListener(wolverineSettings, opts);

            // Publishers
            AddEmailStatusCheckPublisher(wolverineSettings, opts);
        });
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email send queue, enabling
    /// the email service to consume <see cref="SendEmailCommand"/> messages.
    /// This method is invoked only when <see cref="WolverineSettings.EnableSendEmailListener"/> is <c>true</c>.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailSendQueueListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendEmailListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailSendQueueName)} must be configured when {nameof(WolverineSettings.EnableSendEmailListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new SendEmailCommandHandlerPolicy(wolverineSettings));
    }

    /// <summary>
    /// Registers Wolverine publishing rules for <see cref="CheckEmailSendStatusCommand"/>, routing
    /// outbound commands to the Azure Service Bus email status check queue.
    /// This method is invoked only when <see cref="WolverineSettings.EnableEmailStatusCheckListener"/> is <c>true</c>.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailStatusCheckPublisher(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableEmailStatusCheckListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusCheckQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailStatusCheckQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailStatusCheckListener)} is enabled.");
        }

        wolverineOptions.PublishMessage<CheckEmailSendStatusCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.EmailStatusCheckQueueName);
    }

    /// <summary>
    /// Registers the Wolverine listener for the Azure Service Bus email status check queue, enabling
    /// the email service to consume <see cref="CheckEmailSendStatusCommand"/> messages and
    /// poll Azure Communication Services (ACS) for delivery status.
    /// This method is invoked only when <see cref="WolverineSettings.EnableEmailStatusCheckListener"/> is <c>true</c>.
    /// </summary>
    /// <param name="wolverineSettings">The Wolverine settings containing queue names and feature flags.</param>
    /// <param name="wolverineOptions">The Wolverine options to configure.</param>
    private static void AddEmailStatusCheckListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableEmailStatusCheckListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusCheckQueueName))
        {
            throw new InvalidOperationException(
                $"{nameof(WolverineSettings.EmailStatusCheckQueueName)} must be configured when {nameof(WolverineSettings.EnableEmailStatusCheckListener)} is enabled.");
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailStatusCheckQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);

        wolverineOptions.Policies.Add(new CheckEmailSendStatusHandlerPolicy(wolverineSettings));
    }
}
