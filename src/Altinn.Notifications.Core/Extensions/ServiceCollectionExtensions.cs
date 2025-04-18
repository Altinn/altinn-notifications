﻿using Altinn.Notifications.Core.Configuration;
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
            .AddSingleton<IDateTimeService, DateTimeService>()
            .AddSingleton<IOrderProcessingService, OrderProcessingService>()
            .AddSingleton<IEmailOrderProcessingService, EmailOrderProcessingService>()
            .AddSingleton<ISmsOrderProcessingService, SmsOrderProcessingService>()
            .AddSingleton<IPreferredChannelProcessingService, PreferredChannelProcessingService>()
            .AddSingleton<IGetOrderService, GetOrderService>()
            .AddSingleton<IOrderRequestService, OrderRequestService>()
            .AddSingleton<ICancelOrderService, CancelOrderService>()
            .AddSingleton<IEmailNotificationSummaryService, EmailNotificationSummaryService>()
            .AddSingleton<IEmailNotificationService, EmailNotificationService>()
            .AddSingleton<ISmsNotificationService, SmsNotificationService>()
            .AddSingleton<ISmsNotificationSummaryService, SmsNotificationSummaryService>()
            .AddSingleton<IContactPointService, ContactPointService>()
            .AddSingleton<IAltinnServiceUpdateService, AltinnServiceUpdateService>()
            .AddSingleton<INotificationsEmailServiceUpdateService, NotificationsEmailServiceUpdateService>()
            .AddSingleton<IMetricsService, MetricsService>()
            .AddSingleton<INotificationScheduleService, NotificationScheduleService>()
            .AddSingleton<IKeywordsService, KeywordsService>()
            .Configure<KafkaSettings>(config.GetSection("KafkaSettings"))
            .Configure<NotificationConfig>(config.GetSection("NotificationConfig"));
    }
}
