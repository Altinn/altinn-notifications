using Altinn.Notifications.Core.BackgroundQueue;
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
    public static void AddCoreServices(this IServiceCollection services, IConfiguration config)
    {
        _ = config.GetSection("KafkaSettings")
            .Get<KafkaSettings>()
            ?? throw new ArgumentNullException(nameof(config), "Required KafkaSettings is missing from application configuration");

        _ = config.GetSection("NotificationConfig")
            .Get<NotificationConfig>()
            ?? throw new ArgumentNullException(nameof(config), "Required NotificationConfig is missing from application configuration");

        services
            .AddSingleton<IGuidService, GuidService>()
            .AddSingleton<IMetricsService, MetricsService>()
            .AddSingleton<IKeywordsService, KeywordsService>()
            .AddSingleton<IDateTimeService, DateTimeService>()
            .AddSingleton<IGetOrderService, GetOrderService>()
            .AddSingleton<IStatusFeedService, StatusFeedService>()
            .AddSingleton<ICancelOrderService, CancelOrderService>()
            .AddSingleton<IContactPointService, ContactPointService>()
            .AddSingleton<IOrderRequestService, OrderRequestService>()
            .AddSingleton<ISmsPublishTaskQueue, SmsPublishTaskQueue>()
            .AddSingleton<IEmailPublishTaskQueue, EmailPublishTaskQueue>()
            .AddSingleton<ISmsNotificationService, SmsNotificationService>()
            .AddSingleton<IOrderProcessingService, OrderProcessingService>()
            .AddSingleton<IEmailNotificationService, EmailNotificationService>()
            .AddSingleton<IDeadDeliveryReportService, DeadDeliveryReportService>()
            .AddSingleton<ISmsOrderProcessingService, SmsOrderProcessingService>()
            .AddSingleton<IInstantOrderRequestService, InstantOrderRequestService>()
            .AddSingleton<IAltinnServiceUpdateService, AltinnServiceUpdateService>()
            .AddSingleton<INotificationScheduleService, NotificationScheduleService>()
            .AddSingleton<IEmailOrderProcessingService, EmailOrderProcessingService>()
            .AddSingleton<ISmsNotificationSummaryService, SmsNotificationSummaryService>()
            .AddSingleton<IEmailNotificationSummaryService, EmailNotificationSummaryService>()
            .AddSingleton<IPreferredChannelProcessingService, PreferredChannelProcessingService>()
            .AddSingleton<IEmailAndSmsOrderProcessingService, EmailAndSmsOrderProcessingService>()
            .AddSingleton<INotificationDeliveryManifestService, NotificationDeliveryManifestService>()
            .AddSingleton<INotificationsEmailServiceUpdateService, NotificationsEmailServiceUpdateService>()
            .Configure<KafkaSettings>(config.GetSection("KafkaSettings"))
            .Configure<NotificationConfig>(config.GetSection("NotificationConfig"));
    }
}
