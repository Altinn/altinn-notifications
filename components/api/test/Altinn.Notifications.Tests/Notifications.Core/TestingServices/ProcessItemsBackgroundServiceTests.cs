using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class ProcessItemsBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAnyRequiredTaskCountIsZero_ReturnsWithoutProcessing()
    {
        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var scheduleService = new Mock<INotificationScheduleService>(MockBehavior.Strict);

        var config = CreateConfig();
        config.EmailNotificationsProcessLoopConfig.TaskCount = 0;

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            Options.Create(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        await service.ExecuteForTest(CancellationToken.None);

        emailService.Verify(e => e.SendNotification(It.IsAny<CancellationToken>()), Times.Never);
        emailService.Verify(e => e.SendComposedNotification(It.IsAny<CancellationToken>()), Times.Never);
        smsService.Verify(s => s.SendNotifications(It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()), Times.Never);
        orderService.Verify(o => o.TryProcessOrder(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendSmsNow(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRunningAndScheduleAllowsSms_CompletesWithoutExceptions()
    {
        var emailService = new Mock<IEmailNotificationService>();
        emailService.Setup(e => e.SendNotification(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        emailService.Setup(e => e.SendComposedNotification(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var orderService = new Mock<IOrderProcessingService>();
        orderService.Setup(o => o.TryProcessOrder(false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        orderService.Setup(o => o.TryProcessOrder(true, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendSmsNow()).Returns(true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var smsService = new Mock<ISmsNotificationService>();
        smsService
            .Setup(s => s.SendNotifications(It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()))
            .Returns<CancellationToken, SendingTimePolicy>((_, policy) =>
            {
                if (policy == SendingTimePolicy.Daytime)
                {
                    cts.Cancel();
                }

                return Task.FromResult(false);
            });

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            Options.Create(CreateConfig()),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        await service.ExecuteForTest(cts.Token);

        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleDisallowsSms_DoesNotCallDaytimePolicy()
    {
        var emailService = new Mock<IEmailNotificationService>();
        emailService.Setup(e => e.SendNotification(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        emailService.Setup(e => e.SendComposedNotification(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var orderService = new Mock<IOrderProcessingService>();
        orderService.Setup(o => o.TryProcessOrder(false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        orderService.Setup(o => o.TryProcessOrder(true, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendSmsNow()).Returns(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>();
        smsService
            .Setup(s => s.SendNotifications(It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()))
            .Returns<CancellationToken, SendingTimePolicy>((_, policy) =>
            {
                if (policy == SendingTimePolicy.Anytime)
                {
                    cts.Cancel();
                }

                return Task.FromResult(false);
            });

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            Options.Create(CreateConfig()),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        await service.ExecuteForTest(cts.Token);

        smsService.Verify(s => s.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyCancelled_ExitsImmediately()
    {
        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var scheduleService = new Mock<INotificationScheduleService>(MockBehavior.Strict);

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            Options.Create(CreateConfig()),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await service.ExecuteForTest(cts.Token);

        emailService.Verify(e => e.SendNotification(It.IsAny<CancellationToken>()), Times.Never);
        emailService.Verify(e => e.SendComposedNotification(It.IsAny<CancellationToken>()), Times.Never);
        smsService.Verify(s => s.SendNotifications(It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()), Times.Never);
        orderService.Verify(o => o.TryProcessOrder(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendSmsNow(), Times.Never);
    }

    private static NotificationConfig CreateConfig()
    {
        return new NotificationConfig
        {
            EmailNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 1,
                PrimaryTaskIdleDelaySeconds = 0,
                AdditionalTasksIdleDelaySeconds = 0,
                RampUpLimit = 1
            },
            ComposedEmailNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 1,
                PrimaryTaskIdleDelaySeconds = 0,
                AdditionalTasksIdleDelaySeconds = 0,
                RampUpLimit = 1
            },
            SmsDaytimeNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 1,
                PrimaryTaskIdleDelaySeconds = 0,
                AdditionalTasksIdleDelaySeconds = 0,
                RampUpLimit = 1
            },
            SmsAnytimeNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 1,
                PrimaryTaskIdleDelaySeconds = 0,
                AdditionalTasksIdleDelaySeconds = 0,
                RampUpLimit = 1
            },
            PastDueOrdersProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 1,
                PrimaryTaskIdleDelaySeconds = 0,
                AdditionalTasksIdleDelaySeconds = 0,
                RampUpLimit = 1
            },
            RetryOrdersProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 1,
                PrimaryTaskIdleDelaySeconds = 0,
                AdditionalTasksIdleDelaySeconds = 0,
                RampUpLimit = 1
            }
        };
    }

    private sealed class TestableProcessItemsBackgroundService : ProcessItemsBackgroundService
    {
        public TestableProcessItemsBackgroundService(
            IEmailNotificationService emailSendingService,
            ISmsNotificationService smsSendingService,
            IOrderProcessingService orderProcessingService,
            IOptions<NotificationConfig> config,
            ILogger<ProcessItemsBackgroundService> logger,
            INotificationScheduleService notificationScheduleService)
            : base(emailSendingService, smsSendingService, orderProcessingService, config, logger, notificationScheduleService)
        {
        }

        public Task ExecuteForTest(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }
    }
}
