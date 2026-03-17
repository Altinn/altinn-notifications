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
            opts.Discovery.IncludeAssembly(typeof(WolverineServiceCollectionExtensions).Assembly);
            opts.ConfigureNotificationsDefaults(env, wolverineSettings.ServiceBusConnectionString);
            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());

            if (!string.IsNullOrWhiteSpace(wolverineSettings.CheckEmailSendStatusQueueName))
            {
                // Listener: check email send status queue (polling loop)
                if (wolverineSettings.EnableCheckEmailSendStatusListener)
                {
                    opts.ListenToAzureServiceBusQueue(wolverineSettings.CheckEmailSendStatusQueueName)
                        .ListenerCount(wolverineSettings.ListenerCount);
                }

                // Publisher: check email send status queue (re-publish for polling loop)
                if (wolverineSettings.EnableCheckEmailSendStatusPublisher)
                {
                    opts.PublishMessage<CheckEmailSendStatusCommand>()
                        .ToAzureServiceBusQueue(wolverineSettings.CheckEmailSendStatusQueueName);
                }
            }
        });
    }
}
