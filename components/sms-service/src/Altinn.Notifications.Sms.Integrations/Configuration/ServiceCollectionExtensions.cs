using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.LinkMobility;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    /// <param name="environment">The hosting environment, to refuse plain payload logging in Production.</param>
    /// <returns>The given service collection.</returns>
    public static IServiceCollection AddSmsGatewayServices(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(environment);

        SmsGatewaySettings? smsGatewaySettings = config.GetSection(nameof(SmsGatewaySettings)).Get<SmsGatewaySettings>();

        if (smsGatewaySettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required SmsGatewayConfiguration settings are missing from application configuration.");
        }

        if (smsGatewaySettings.TimeoutInSeconds <= 0)
        {
            throw new InvalidOperationException($"{nameof(SmsGatewaySettings.TimeoutInSeconds)} must be greater than 0.");
        }

        if (smsGatewaySettings.LogPayloads == PayloadLogMode.Plain && environment.IsProduction())
        {
            throw new InvalidOperationException($"{nameof(SmsGatewaySettings)}:{nameof(SmsGatewaySettings.LogPayloads)}=Plain logs recipient numbers and message texts, and is not allowed in Production.");
        }

        services
            .AddSingleton(smsGatewaySettings);

        if (smsGatewaySettings.MockSmsClient)
        {
            // Should only be used for local development and testing, not in production.
            services.AddSingleton<ISmsClient, MockSmsClient>();
        }
        else
        {
            services.AddSingleton<ISmsClient, SmsClient>();
        }

        var gatewayClient = services.AddHttpClient<IAltinnGatewayClient, AltinnGatewayClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(smsGatewaySettings.TimeoutInSeconds);
        });

        if (smsGatewaySettings.LogPayloads == PayloadLogMode.Plain)
        {
            services.AddTransient<PayloadLoggingHandler>();
            services.AddHostedService<PayloadLoggingWarning>();
            gatewayClient.AddHttpMessageHandler<PayloadLoggingHandler>();
        }

        return services;
    }
}
