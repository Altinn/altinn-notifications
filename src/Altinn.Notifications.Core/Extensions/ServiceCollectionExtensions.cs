using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Configuration;
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
    /// <param name="config">the configuration collection</param>
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration config)
    {
        // .AddSingleton<IEmailNotificationOrderService, EmailNotificationOrderService>()
        return services
              .AddSingleton<IGuidService, GuidService>()
              .AddSingleton<IDateTimeService, DateTimeService>()
              .AddSingleton<IOrderProcessingService, OrderProcessingService>()
              .Configure<KafkaTopicSettings>(config.GetSection("KafkaSettings:Topics"))
              .Configure<NotificationOrderConfig>(config.GetSection("NotificationOrderConfig"));
    }
}