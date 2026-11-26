using System.Collections.Concurrent;

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
        config.EmailAnytimeNotificationsProcessLoopConfig.TaskCount = 0;

        var service = new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            Options.Create(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);

        await service.ExecuteForTest(CancellationToken.None);

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
            Options.Create(config),
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
            Options.Create(config),
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
            Options.Create(config),
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
            Options.Create(config),
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
            Options.Create(config),
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
            Options.Create(CreateConfig()),
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
            Options.Create(CreateConfig()),
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

    [Fact]
    public async Task ExecuteAsync_EmailAnytimeRetryLoop_CallsAnytimeRetryWithoutScheduleCheck()
    {
        var config = CreateConfig();
        config.EmailAnytimeRetryNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var scheduleService = new Mock<INotificationScheduleService>(MockBehavior.Strict);

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        emailService
            .Setup(e => e.SendRetryNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        emailService.Verify(e => e.SendRetryNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        scheduleService.Verify(s => s.CanSendEmailNow(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmailDaytimeRetryLoop_WhenScheduleAllows_CallsDaytimeRetry()
    {
        var config = CreateConfig();
        config.EmailDaytimeRetryNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        emailService
            .Setup(e => e.SendRetryNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendEmailNow()).Returns(true);

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        emailService.Verify(e => e.SendRetryNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_EmailDaytimeRetryLoop_WhenScheduleDisallows_DoesNotCallRetry()
    {
        var config = CreateConfig();
        config.EmailDaytimeRetryNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService
            .Setup(s => s.CanSendEmailNow())
            .Returns(() =>
            {
                cts.Cancel();
                return false;
            });

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        emailService.Verify(e => e.SendRetryNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendEmailNow(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ComposedEmailRetryLoop_CallsComposedRetry()
    {
        var config = CreateConfig();
        config.ComposedEmailRetryNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var scheduleService = new Mock<INotificationScheduleService>(MockBehavior.Strict);

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        emailService
            .Setup(e => e.SendComposedRetryNotification(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(_ =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        emailService.Verify(e => e.SendComposedRetryNotification(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SmsAnytimeRetryLoop_CallsAnytimeRetryWithoutScheduleCheck()
    {
        var config = CreateConfig();
        config.SmsAnytimeRetryNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var scheduleService = new Mock<INotificationScheduleService>(MockBehavior.Strict);

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        smsService
            .Setup(s => s.SendRetryNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        smsService.Verify(s => s.SendRetryNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        scheduleService.Verify(s => s.CanSendSmsNow(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SmsDaytimeRetryLoop_WhenScheduleAllows_CallsDaytimeRetry()
    {
        var config = CreateConfig();
        config.SmsDaytimeRetryNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        smsService
            .Setup(s => s.SendRetryNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()))
            .Returns<SendingTimePolicy, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                return Task.FromResult(false);
            });

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendSmsNow()).Returns(true);

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        smsService.Verify(s => s.SendRetryNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SmsDaytimeRetryLoop_WhenScheduleDisallows_DoesNotCallRetry()
    {
        var config = CreateConfig();
        config.SmsDaytimeRetryNotificationsProcessLoopConfig.TaskCount = 1;
        SetZeroIdleDelays(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService
            .Setup(s => s.CanSendSmsNow())
            .Returns(() =>
            {
                cts.Cancel();
                return false;
            });

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        smsService.Verify(s => s.SendRetryNotification(It.IsAny<SendingTimePolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        scheduleService.Verify(s => s.CanSendSmsNow(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithAdditionalTasksOnEveryLoop_RunsAllLoopsUntilCancelled()
    {
        var config = CreateConfig();
        SetZeroIdleDelays(config);
        foreach (var loopConfig in GetAllLoopConfigs(config))
        {
            loopConfig.TaskCount = 2;

            // A zero delay completes synchronously, so an idle additional task would spin on the thread starting the loops.
            loopConfig.AdditionalTasksIdleDelaySeconds = 1;
        }

        // Generous safety net only: the test ends as soon as every loop has been called enough times.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        const int loopCount = 12;
        const int callsPerLoop = 4;
        var callCounts = new ConcurrentDictionary<string, int>();

        // Only the primary task processes while no items were returned lately, the additional tasks stay idle.
        // The short delay stops the zero-idle-delay primary loops from spinning and starving each other.
        async Task<bool> Track(string name)
        {
            await Task.Delay(1);

            callCounts.AddOrUpdate(name, 1, (_, current) => current + 1);
            if (callCounts.Count == loopCount && callCounts.Values.All(c => c >= callsPerLoop))
            {
                cts.Cancel();
            }

            return false;
        }

        var emailService = new Mock<IEmailNotificationService>(MockBehavior.Strict);
        emailService.Setup(e => e.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>())).Returns(() => Track("email-daytime"));
        emailService.Setup(e => e.SendNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>())).Returns(() => Track("email-anytime"));
        emailService.Setup(e => e.SendComposedNotification(It.IsAny<CancellationToken>())).Returns(() => Track("email-composed"));
        emailService.Setup(e => e.SendRetryNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>())).Returns(() => Track("email-daytime-retry"));
        emailService.Setup(e => e.SendRetryNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>())).Returns(() => Track("email-anytime-retry"));
        emailService.Setup(e => e.SendComposedRetryNotification(It.IsAny<CancellationToken>())).Returns(() => Track("email-composed-retry"));

        var smsService = new Mock<ISmsNotificationService>(MockBehavior.Strict);
        smsService.Setup(s => s.SendNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>())).Returns(() => Track("sms-daytime"));
        smsService.Setup(s => s.SendNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>())).Returns(() => Track("sms-anytime"));
        smsService.Setup(s => s.SendRetryNotification(SendingTimePolicy.Daytime, It.IsAny<CancellationToken>())).Returns(() => Track("sms-daytime-retry"));
        smsService.Setup(s => s.SendRetryNotification(SendingTimePolicy.Anytime, It.IsAny<CancellationToken>())).Returns(() => Track("sms-anytime-retry"));

        var orderService = new Mock<IOrderProcessingService>(MockBehavior.Strict);
        orderService.Setup(o => o.TryProcessOrder(false, It.IsAny<CancellationToken>())).Returns(() => Track("order-past-due"));
        orderService.Setup(o => o.TryProcessOrder(true, It.IsAny<CancellationToken>())).Returns(() => Track("order-retry"));

        var scheduleService = new Mock<INotificationScheduleService>();
        scheduleService.Setup(s => s.CanSendEmailNow()).Returns(true);
        scheduleService.Setup(s => s.CanSendSmsNow()).Returns(true);

        var service = CreateService(emailService, smsService, orderService, scheduleService, config);

        await service.ExecuteForTest(cts.Token);

        Assert.Equal(loopCount, callCounts.Count);
        Assert.All(callCounts.Values, count => Assert.True(count >= callsPerLoop));
    }

    private static TestableProcessItemsBackgroundService CreateService(
        Mock<IEmailNotificationService> emailService,
        Mock<ISmsNotificationService> smsService,
        Mock<IOrderProcessingService> orderService,
        Mock<INotificationScheduleService> scheduleService,
        NotificationConfig config)
    {
        return new TestableProcessItemsBackgroundService(
            emailService.Object,
            smsService.Object,
            orderService.Object,
            Options.Create(config),
            new Mock<ILogger<ProcessItemsBackgroundService>>().Object,
            scheduleService.Object);
    }

    private static ProcessLoopConfig[] GetAllLoopConfigs(NotificationConfig config)
    {
        return
        [
            config.EmailDaytimeNotificationsProcessLoopConfig,
            config.EmailAnytimeNotificationsProcessLoopConfig,
            config.ComposedEmailNotificationsProcessLoopConfig,
            config.SmsDaytimeNotificationsProcessLoopConfig,
            config.SmsAnytimeNotificationsProcessLoopConfig,
            config.EmailDaytimeRetryNotificationsProcessLoopConfig,
            config.EmailAnytimeRetryNotificationsProcessLoopConfig,
            config.ComposedEmailRetryNotificationsProcessLoopConfig,
            config.SmsDaytimeRetryNotificationsProcessLoopConfig,
            config.SmsAnytimeRetryNotificationsProcessLoopConfig,
            config.PastDueOrdersProcessLoopConfig,
            config.RetryOrdersProcessLoopConfig
        ];
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
            },
            EmailDaytimeRetryNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            EmailAnytimeRetryNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            ComposedEmailRetryNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            SmsDaytimeRetryNotificationsProcessLoopConfig = new ProcessLoopConfig
            {
                TaskCount = 0
            },
            SmsAnytimeRetryNotificationsProcessLoopConfig = new ProcessLoopConfig
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

        foreach (var retryConfig in new[]
        {
            config.EmailDaytimeRetryNotificationsProcessLoopConfig,
            config.EmailAnytimeRetryNotificationsProcessLoopConfig,
            config.ComposedEmailRetryNotificationsProcessLoopConfig,
            config.SmsDaytimeRetryNotificationsProcessLoopConfig,
            config.SmsAnytimeRetryNotificationsProcessLoopConfig
        })
        {
            retryConfig.PrimaryTaskIdleDelaySeconds = 0;
            retryConfig.AdditionalTasksIdleDelaySeconds = 0;
            retryConfig.RampUpLimit = 1;
        }
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
