using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.Configuration;
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

            // Publishers: none configured yet.
            ConfigureCheckEmailSendStatus(opts, wolverineSettings);
        });
    }

    /// <summary>
    /// Adds the email send queue listener.
    /// </summary>
    /// <param name="wolverineSettings">The wolverine settings.</param>
    /// <param name="wolverineOptions">The opts.</param>
    private static void AddEmailSendQueueListener(WolverineSettings wolverineSettings, WolverineOptions wolverineOptions)
    {
        if (!wolverineSettings.EnableSendEmailListener)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.EmailSendQueueName))
        {
            return;
        }

        wolverineOptions.ListenToAzureServiceBusQueue(wolverineSettings.EmailSendQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);
    }

    /// <summary>
    /// Configures listener and publisher settings for the check-email-send-status queue.
    /// </summary>
    /// <param name="opts">The Wolverine options.</param>
    /// <param name="wolverineSettings">The Wolverine settings.</param>
    private static void ConfigureCheckEmailSendStatus(WolverineOptions opts, WolverineSettings wolverineSettings)
    {
        if (!wolverineSettings.EnableCheckEmailSendStatus)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(wolverineSettings.CheckEmailSendStatusQueueName))
        {
            return;
        }

        opts.PublishMessage<CheckEmailSendStatusCommand>()
            .ToAzureServiceBusQueue(wolverineSettings.CheckEmailSendStatusQueueName);

        opts.ListenToAzureServiceBusQueue(wolverineSettings.CheckEmailSendStatusQueueName)
            .ListenerCount(wolverineSettings.ListenerCount);
    }
}
