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
    public static IServiceCollection AddSmsGatewayServices(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        SmsGatewaySettings? smsGatewaySettings = config.GetSection(nameof(SmsGatewaySettings)).Get<SmsGatewaySettings>();

        if (smsGatewaySettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required SmsGatewayConfiguration settings are missing from application configuration.");
        }

        if (smsGatewaySettings.TimeoutInSeconds <= 0)
        {
            throw new InvalidOperationException($"{nameof(SmsGatewaySettings.TimeoutInSeconds)} must be greater than 0.");
        }

        services
            .AddSingleton<ISmsClient, SmsClient>()
            .AddSingleton(smsGatewaySettings);

        services.AddHttpClient<IAltinnGatewayClient, AltinnGatewayClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(smsGatewaySettings.TimeoutInSeconds);
        });

        return services;
    }
}
