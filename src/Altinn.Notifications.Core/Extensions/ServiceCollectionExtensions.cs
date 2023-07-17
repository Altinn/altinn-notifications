using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Core.Extensions;

/// <summary>
/// Extension class for <see cref="IServiceCollection"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core services and configurations to DI container.
    /// </summary>
    /// <param name="services">service collection.</param>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IGuidService, GuidService>();
    }
}