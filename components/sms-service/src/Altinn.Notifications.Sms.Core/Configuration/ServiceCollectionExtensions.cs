using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Status;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Core.Configuration;

/// <summary>
/// This class is responsible for holding extension methods for program startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add necessary core services and configuration to the service collection.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The given service collection.</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services
            .AddSingleton<IStatusService, StatusService>()
            .AddSingleton<ISendingService, SendingService>();

        return services;
    }
}
