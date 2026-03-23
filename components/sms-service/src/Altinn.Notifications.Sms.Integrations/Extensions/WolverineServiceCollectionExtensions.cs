using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Shared.Configuration;
using Altinn.Notifications.Shared.Extensions;
using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Wolverine;
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

        SendSmsCommandHandler.Settings = wolverineSettings;

        services.Configure<WolverineSettings>(wolverineSection);

        services.AddWolverine(opts =>
        {
            opts.ConfigureNotificationsDefaults(env, wolverineSettings.ServiceBusConnectionString);
            opts.Policies.AllListeners(x => x.ProcessInline());
            opts.Policies.AllSenders(x => x.SendInline());

            if (wolverineSettings.AcceptSmsNotificationsViaWolverine && !string.IsNullOrWhiteSpace(wolverineSettings.SmsSendQueueName))
            {
                opts.ListenToAzureServiceBusQueue(wolverineSettings.SmsSendQueueName)
                    .ListenerCount(wolverineSettings.ListenerCount);
            }
        });
    }
}
