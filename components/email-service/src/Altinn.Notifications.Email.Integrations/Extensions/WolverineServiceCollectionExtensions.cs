using System.Diagnostics.CodeAnalysis;

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
    /// Adds Wolverine and configures Azure Service Bus transport when
    /// <see cref="WolverineSettingsBase.EnableServiceBus"/> is <c>true</c>.
    /// When disabled, Wolverine is still registered with inline policies so that
    /// <see cref="IMessageBus"/> can be resolved by future handlers and publishers.
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

        services.AddWolverine(opts =>
        {
            if (wolverineSettings.EnableServiceBus)
            {
                // Defaults
                opts.ConfigureNotificationsDefaults(env, wolverineSettings.ServiceBusConnectionString);

                // Listeners (ASB queues will be auto-provisioned in production)
                if (!string.IsNullOrWhiteSpace(wolverineSettings.SendEmailQueueName))
                {
                    opts.ListenToAzureServiceBusQueue(wolverineSettings.SendEmailQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);
                }

                if (!string.IsNullOrWhiteSpace(wolverineSettings.EmailSendingAcceptedQueueName))
                {
                    opts.ListenToAzureServiceBusQueue(wolverineSettings.EmailSendingAcceptedQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);
                }

                // Publishers
                // Note: the email service currently does not publish any messages to ASB.
            }

            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());
        });
    }
}
