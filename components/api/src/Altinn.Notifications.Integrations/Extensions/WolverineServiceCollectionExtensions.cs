using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    /// Adds Wolverine and configures Azure Service Bus transport when
    /// <see cref="Shared.Configuration.WolverineSettings.EnableServiceBus"/> is <c>true</c>.
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
                if (!string.IsNullOrWhiteSpace(wolverineSettings.EmailDeliveryReportQueueName))
                {
                    opts.ListenToAzureServiceBusQueue(wolverineSettings.EmailDeliveryReportQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);
                }

                if (!string.IsNullOrWhiteSpace(wolverineSettings.SmsDeliveryReportQueueName))
                {
                    opts.ListenToAzureServiceBusQueue(wolverineSettings.SmsDeliveryReportQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);
                }

                if (!string.IsNullOrWhiteSpace(wolverineSettings.EmailStatusUpdatedQueueName))
                {
                    opts.ListenToAzureServiceBusQueue(wolverineSettings.EmailStatusUpdatedQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);
                }

                // Publishers
                // Note: the API currently does not publish any messages to ASB.
            }

            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());
        });
    }
}
