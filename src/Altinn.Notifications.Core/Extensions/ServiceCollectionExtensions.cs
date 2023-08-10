using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations.Consumers;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        KafkaSettings? kafkaSettings = config.GetSection("KafkaSettings").Get<KafkaSettings>();

        if (kafkaSettings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required KafkaSettings is missing from application configuration");
        }

        NotificationOrderConfig? settings = config.GetSection("NotificationOrderConfig").Get<NotificationOrderConfig>();

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(config), "Required NotificationOrderConfig is missing from application configuration");
        }

        return services
              .AddSingleton<IGuidService, GuidService>()
              .AddSingleton<IDateTimeService, DateTimeService>()
              .AddSingleton<IOrderProcessingService, OrderProcessingService>()
              .AddSingleton<IEmailNotificationOrderService, EmailNotificationOrderService>()
              .AddSingleton<IEmailNotificationService, EmailNotificationService>()
              .AddSingleton<IGetOrderService, GetOrderService>()
              .AddSingleton<IHostedService, PastDueOrdersConsumer>()
              .AddSingleton<IHostedService, PastDueOrdersConsumerRetry>()
              .Configure<KafkaSettings>(config.GetSection("KafkaSettings"))
              .Configure<NotificationOrderConfig>(config.GetSection("NotificationOrderConfig"));
    }
}