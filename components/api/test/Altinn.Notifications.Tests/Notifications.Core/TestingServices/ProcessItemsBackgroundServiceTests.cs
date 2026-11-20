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
    public async Task ReconcileAsync_ScaleDownAcceptsWorkerCancellationAndCanScaleUpAgain()
    {
        var config = CreateConfig();
        var loop = config.EmailAnytimeNotificationsProcessLoopConfig;
        CancellationToken stoppingToken = TestContext.Current.CancellationToken;
        var startedWorkers = new List<CancellationToken>();
        var pool = new ProcessItemsBackgroundService.WorkerPool(
            c => c.EmailAnytimeNotificationsProcessLoopConfig,
            (_, _, token) =>
            {
                startedWorkers.Add(token);
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        try
        {
            loop.TaskCount = 1;
            await pool.ReconcileAsync(config, 1, stoppingToken);
            CancellationToken firstWorker = Assert.Single(startedWorkers);

            loop.TaskCount = 0;
            await pool.ReconcileAsync(config, 1, stoppingToken);
            Assert.True(firstWorker.IsCancellationRequested);

            loop.TaskCount = 1;
            await pool.ReconcileAsync(config, 1, stoppingToken);
            Assert.Equal(2, startedWorkers.Count);
            Assert.False(startedWorkers[1].IsCancellationRequested);
        }
        finally
        {
            await pool.StopAsync();
        }
    }

    [Fact]
    public async Task ReconcileAsync_ScaleDownPropagatesUnrelatedCancellation()
    {
        var config = CreateConfig();
        var loop = config.EmailAnytimeNotificationsProcessLoopConfig;
        CancellationToken stoppingToken = TestContext.Current.CancellationToken;
        var unrelatedToken = new CancellationToken(canceled: true);
        var pool = new ProcessItemsBackgroundService.WorkerPool(
            c => c.EmailAnytimeNotificationsProcessLoopConfig,
            (_, _, _) => Task.FromCanceled(unrelatedToken));

        loop.TaskCount = 1;
        await pool.ReconcileAsync(config, 1, stoppingToken);

        loop.TaskCount = 0;
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pool.ReconcileAsync(config, 1, stoppingToken));
        Assert.Equal(unrelatedToken, exception.CancellationToken);
    }

    [Fact]
    public async Task ReconcileAsync_ScaleDownPropagatesWorkerFailure()
    {
        var config = CreateConfig();
        var loop = config.EmailAnytimeNotificationsProcessLoopConfig;
        CancellationToken stoppingToken = TestContext.Current.CancellationToken;
        var failure = new InvalidOperationException("Worker failed.");
        var pool = new ProcessItemsBackgroundService.WorkerPool(
            c => c.EmailAnytimeNotificationsProcessLoopConfig,
            (_, _, _) => Task.FromException(failure));

        loop.TaskCount = 1;
        await pool.ReconcileAsync(config, 1, stoppingToken);

        loop.TaskCount = 0;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => pool.ReconcileAsync(config, 1, stoppingToken));
        Assert.Same(failure, exception);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public async Task ExecuteAsync_WhenNoWorkersAreAssignedToPod_DoesNotProcessUntilStopped(int taskCount, int podCount)
    {
        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var scheduleService = new Mock<INotificationScheduleService>(MockBehavior.Strict);

        var config = CreateConfig();
        config.EmailAnytimeNotificationsProcessLoopConfig.TaskCount = taskCount;

        using var cts = new CancellationTokenSource();
        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            CreateOptionsMonitor(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object,
            podCount);

        Task execution = service.ExecuteForTest(cts.Token);
        await cts.CancelAsync();
        await execution;

        emailService.Verify(e => e.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        emailService.Verify(e => e.SendComposedNotification(It.IsAny<CancellationToken>()), Times.Never);
        smsService.Verify(s => s.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        orderService.Verify(o => o.TryProcessOrder(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendSmsNow(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDaytimeScheduleAllowsSms_CallsDaytimeSmsPolicy()
    {
        // Arrange
        var config = CreateConfig();
        config.SmsDaytimeNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);

        var smsService = new Mock<ISmsNotificationService>();
        smsService
            .Setup(s => s.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendSmsNow()).Returns(true);

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            CreateOptionsMonitor(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        // Act
        await service.ExecuteForTest(cts.Token);

        // Assert
        smsService.Verify(s => s.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        scheduleService.Verify(s => s.CanSendSmsNow(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDaytimeScheduleAllowsEmail_CallsDaytimeEmailPolicy()
    {
        // Arrange
        var config = CreateConfig();
        config.EmailDaytimeNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);

        var emailService = new Mock<IEmailNotificationService>();
        emailService
            .Setup(e => e.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendEmailNow()).Returns(true);

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            CreateOptionsMonitor(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        // Act
        await service.ExecuteForTest(cts.Token);

        // Assert
        emailService.Verify(e => e.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        emailService.Verify(e => e.SendNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendEmailNow(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleDisallowsEmail_DoesNotCallDaytimeEmailPolicy()
    {
        // Arrange
        var config = CreateConfig();
        config.EmailDaytimeNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var emailService = new Mock<IEmailNotificationService>();

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService
            .Setup(s => s.CanSendEmailNow())
            .Returns(() =>
            {
                cts.Cancel();
                return false;
            });

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            CreateOptionsMonitor(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        // Act
        await service.ExecuteForTest(cts.Token);

        // Assert
        emailService.Verify(e => e.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendEmailNow(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_EmailAnytimeLoop_CallsAnytimeEmailPolicyWithoutScheduleCheck()
    {
        // Arrange
        var config = CreateConfig();
        config.EmailAnytimeNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var scheduleService = new Mock<INotificationScheduleService>(MockBehavior.Strict);

        var emailService = new Mock<IEmailNotificationService>();
        emailService
            .Setup(e => e.SendNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            CreateOptionsMonitor(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        // Act
        await service.ExecuteForTest(cts.Token);

        // Assert
        emailService.Verify(e => e.SendNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        emailService.Verify(e => e.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendEmailNow(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessLoopThrows_LogsError()
    {
        // Arrange
        var config = CreateConfig();
        config.EmailAnytimeNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var emailService = new Mock<IEmailNotificationService>();
        emailService
            .Setup(e => e.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cts.Cancel();
                throw new InvalidOperationException("loop failure");
            });

        var loggerMock = new Mock<ILogger<ProcessItemsBackgroundService>>();

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            new Mock<ISmsNotificationService>(MockBehavior.Strict).Object,
            new Mock<IOrderProcessingService>(MockBehavior.Strict).Object,
            CreateOptionsMonitor(config),
            loggerMock.Object,
            new Mock<INotificationScheduleService>(MockBehavior.Strict).Object);

        // Act
        await service.ExecuteForTest(cts.Token);

        // Assert
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleDisallowsSms_DoesNotCallDaytimePolicy()
    {
        var emailService = new Mock<IEmailNotificationService>();
        emailService.Setup(e => e.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        emailService.Setup(e => e.SendComposedNotification(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var orderService = new Mock<IOrderProcessingService>();
        orderService.Setup(o => o.TryProcessOrder(false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        orderService.Setup(o => o.TryProcessOrder(true, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendSmsNow()).Returns(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>();
        smsService
            .Setup(s => s.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((policy, _) =>
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
            CreateOptionsMonitor(CreateConfig()),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        await service.ExecuteForTest(cts.Token);

        smsService.Verify(s => s.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()), Times.Never);
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
            CreateOptionsMonitor(CreateConfig()),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await service.ExecuteForTest(cts.Token);

        emailService.Verify(e => e.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        emailService.Verify(e => e.SendComposedNotification(It.IsAny<CancellationToken>()), Times.Never);
        smsService.Verify(s => s.SendNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        orderService.Verify(o => o.TryProcessOrder(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendSmsNow(), Times.Never);
    }

    private static IOptionsMonitor<NotificationConfig> CreateOptionsMonitor(NotificationConfig config)
    {
        var optionsMonitor = new Mock<IOptionsMonitor<NotificationConfig>>();
        optionsMonitor.Setup(monitor => monitor.CurrentValue).Returns(config);
        return optionsMonitor.Object;
    }

    private static NotificationConfig CreateConfig()
    {
        return new NotificationConfig
        {
            EmailAnytimeNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            EmailDaytimeNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            ComposedEmailNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            SmsDaytimeNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            SmsAnytimeNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            PastDueOrdersProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            RetryOrdersProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            }
        };
    }

    private static void SetZeroIdleDelays(NotificationConfig config)
    {
        config.EmailAnytimeNotificationsProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 0;
        config.EmailAnytimeNotificationsProcessLoopConfig.AdditionalTasksIdleDelaySeconds = 0;
        config.EmailAnytimeNotificationsProcessLoopConfig.RampUpLimit = 1;

        config.EmailDaytimeNotificationsProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 0;
        config.EmailDaytimeNotificationsProcessLoopConfig.AdditionalTasksIdleDelaySeconds = 0;
        config.EmailDaytimeNotificationsProcessLoopConfig.RampUpLimit = 1;

        config.ComposedEmailNotificationsProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 0;
        config.ComposedEmailNotificationsProcessLoopConfig.AdditionalTasksIdleDelaySeconds = 0;
        config.ComposedEmailNotificationsProcessLoopConfig.RampUpLimit = 1;

        config.SmsDaytimeNotificationsProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 0;
        config.SmsDaytimeNotificationsProcessLoopConfig.AdditionalTasksIdleDelaySeconds = 0;
        config.SmsDaytimeNotificationsProcessLoopConfig.RampUpLimit = 1;

        config.SmsAnytimeNotificationsProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 0;
        config.SmsAnytimeNotificationsProcessLoopConfig.AdditionalTasksIdleDelaySeconds = 0;
        config.SmsAnytimeNotificationsProcessLoopConfig.RampUpLimit = 1;

        config.PastDueOrdersProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 0;
        config.PastDueOrdersProcessLoopConfig.AdditionalTasksIdleDelaySeconds = 0;
        config.PastDueOrdersProcessLoopConfig.RampUpLimit = 1;

        config.RetryOrdersProcessLoopConfig.PrimaryTaskIdleDelaySeconds = 0;
        config.RetryOrdersProcessLoopConfig.AdditionalTasksIdleDelaySeconds = 0;
        config.RetryOrdersProcessLoopConfig.RampUpLimit = 1;
    }

    private sealed class TestableProcessItemsBackgroundService : ProcessItemsBackgroundService
    {
        public TestableProcessItemsBackgroundService(
            IEmailNotificationService emailSendingService,
            ISmsNotificationService smsSendingService,
            IOrderProcessingService orderProcessingService,
            IOptionsMonitor<NotificationConfig> config,
            ILogger<ProcessItemsBackgroundService> logger,
            INotificationScheduleService notificationScheduleService,
            int podCount = 1)
            : base(emailSendingService, smsSendingService, orderProcessingService, config, logger, notificationScheduleService, CreateDynamicConfigService(podCount))
        {
        }

        private static IDynamicConfigService CreateDynamicConfigService(int podCount)
        {
            var service = new Mock<IDynamicConfigService>();
            service.Setup(s => s.GetDynamicConfig(It.IsAny<NotificationConfig>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NotificationConfig staticConfig, TimeSpan _, CancellationToken _) => (staticConfig, podCount));
            return service.Object;
        }

        public Task ExecuteForTest(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }
    }
}
