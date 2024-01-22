using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.LinkMobility;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

/// <summary>
/// This class is responsible for holding extension methods for program startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add necessary integration services and configuration to the service collection.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The given service collection.</returns>
    public static IServiceCollection AddIntegrationServices(this IServiceCollection services, IConfiguration config)
    {
        SmsGatewayConfiguration smsGatewaySettings = config!.GetSection(nameof(SmsGatewayConfiguration)).Get<SmsGatewayConfiguration>()!;

        if (smsGatewaySettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required SmsGatewayConfiguration settings is missing from application configuration.");
        }

        services.AddSingleton<ISmsClient, SmsClient>()
                .AddSingleton<IAltinnGatewayClient, AltinnGatewayClient>()
                .AddSingleton(smsGatewaySettings);
        return services;
    }
}
