using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class OrderProcessingServiceTests
{
    [Fact]
    public async Task StartProcessingPastDueOrders_NoOrderFound_RollsBackAndReturnsFalse()
    {
        var unitOfWork = new UnitOfWork();
        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrder?)null);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        var result = await service.StartProcessingPastDueOrders(false, TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepositoryMock.Verify(u => u.StartUnitOfWork(), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_OrderFound_ProcessesAndCommits()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Sms);
        var smsResult = new SmsOrderProcessingResult([], null);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        orderRepositoryMock
            .Setup(r => r.PersistProcessingResultAsync(
                unitOfWork,
                order,
                It.IsAny<EmailOrderProcessingResult>(),
                It.IsAny<SmsOrderProcessingResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ReturnsAsync(smsResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object,
            smsOrderProcessingService: smsProcessingServiceMock.Object);

        var result = await service.StartProcessingPastDueOrders(true, TestContext.Current.CancellationToken);

        Assert.True(result);
        orderRepositoryMock.Verify(r => r.GetNextPastDueOrder(unitOfWork, true, It.IsAny<CancellationToken>()), Times.Once);
        smsProcessingServiceMock.Verify(s => s.ProcessOrder(order), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_WhenRepositoryThrows_RollsBackAndReturnsFalse()
    {
        var unitOfWork = new UnitOfWork();
        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("failed"));

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        var result = await service.StartProcessingPastDueOrders(false, TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithoutCondition_PersistsSmsResult()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Sms);
        var smsResult = new SmsOrderProcessingResult([], null);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.PersistProcessingResultAsync(unitOfWork, order, It.IsAny<EmailOrderProcessingResult>(), smsResult, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ReturnsAsync(smsResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            smsOrderProcessingService: smsProcessingServiceMock.Object);

        await service.ProcessOrder(order, unitOfWork);

        smsProcessingServiceMock.Verify(s => s.ProcessOrder(order), Times.Once);
        orderRepositoryMock.Verify(
            r => r.PersistProcessingResultAsync(unitOfWork, order, It.IsAny<EmailOrderProcessingResult>(), smsResult, It.IsAny<CancellationToken>()),
            Times.Once);
        orderRepositoryMock.Verify(
            r => r.SetOrderSendConditionNotMetAsync(It.IsAny<UnitOfWork>(), It.IsAny<NotificationOrder>(), It.IsAny<OrderProcessingStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithoutCondition_PersistsEmailResult()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Email);
        var emailResult = new EmailOrderProcessingResult([], null);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.PersistProcessingResultAsync(unitOfWork, order, emailResult, It.IsAny<SmsOrderProcessingResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();
        emailProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ReturnsAsync(emailResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            emailOrderProcessingService: emailProcessingServiceMock.Object);

        await service.ProcessOrder(order, unitOfWork);

        emailProcessingServiceMock.Verify(s => s.ProcessOrder(order), Times.Once);
        orderRepositoryMock.Verify(
            r => r.PersistProcessingResultAsync(unitOfWork, order, emailResult, It.IsAny<SmsOrderProcessingResult>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithoutCondition_PersistsCombinedResult()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.EmailAndSms);
        var orderProcessingResult = new OrderProcessingResult(new EmailOrderProcessingResult([], null), new SmsOrderProcessingResult([], null));

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.PersistProcessingResultAsync(
                unitOfWork,
                order,
                orderProcessingResult.EmailOrderProcessingResult,
                orderProcessingResult.SmsOrderProcessingResult,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var emailAndSmsOrderProcessingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();
        emailAndSmsOrderProcessingServiceMock.Setup(s => s.ProcessOrderAsync(order)).ReturnsAsync(orderProcessingResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            emailAndSmsOrderProcessingService: emailAndSmsOrderProcessingServiceMock.Object);

        await service.ProcessOrder(order, unitOfWork);

        emailAndSmsOrderProcessingServiceMock.Verify(s => s.ProcessOrderAsync(order), Times.Once);
        orderRepositoryMock.Verify(
            r => r.PersistProcessingResultAsync(
                unitOfWork,
                order,
                orderProcessingResult.EmailOrderProcessingResult,
                orderProcessingResult.SmsOrderProcessingResult,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithoutCondition_PersistsCombinedResult(NotificationChannel notificationChannel)
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(notificationChannel);
        var orderProcessingResult = new OrderProcessingResult(new EmailOrderProcessingResult([], null), new SmsOrderProcessingResult([], null));

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.PersistProcessingResultAsync(
                unitOfWork,
                order,
                orderProcessingResult.EmailOrderProcessingResult,
                orderProcessingResult.SmsOrderProcessingResult,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var preferredChannelProcessingServiceMock = new Mock<IPreferredChannelProcessingService>();
        preferredChannelProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ReturnsAsync(orderProcessingResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            preferredChannelProcessingService: preferredChannelProcessingServiceMock.Object);

        await service.ProcessOrder(order, unitOfWork);

        preferredChannelProcessingServiceMock.Verify(s => s.ProcessOrder(order), Times.Once);
        orderRepositoryMock.Verify(
            r => r.PersistProcessingResultAsync(
                unitOfWork,
                order,
                orderProcessingResult.EmailOrderProcessingResult,
                orderProcessingResult.SmsOrderProcessingResult,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_WhenConditionIsFalse_SetsSendConditionNotMet()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Email, conditionEndpoint: new Uri("https://condition.test"));

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock
            .Setup(c => c.CheckSendCondition(order.ConditionEndpoint!))
            .ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        var emailOrderProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            conditionClient: conditionClientMock.Object,
            emailOrderProcessingService: emailOrderProcessingServiceMock.Object);

        await service.ProcessOrder(order, unitOfWork);

        orderRepositoryMock.Verify(
            r => r.SetOrderSendConditionNotMetAsync(unitOfWork, order, OrderProcessingStatus.SendConditionNotMet, It.IsAny<CancellationToken>()),
            Times.Once);
        orderRepositoryMock.Verify(
            r => r.PersistProcessingResultAsync(It.IsAny<UnitOfWork>(), It.IsAny<NotificationOrder>(), It.IsAny<EmailOrderProcessingResult>(), It.IsAny<SmsOrderProcessingResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        emailOrderProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_WhenConditionCheckFailsAndOrderIsNotRetrying_SetsRetrying()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Email, conditionEndpoint: new Uri("https://condition.test"));

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock
            .Setup(c => c.CheckSendCondition(order.ConditionEndpoint!))
            .ReturnsAsync(new ConditionClientError { StatusCode = 500, Message = "failed" });

        var orderRepositoryMock = new Mock<IOrderRepository>();
        var emailOrderProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            conditionClient: conditionClientMock.Object,
            emailOrderProcessingService: emailOrderProcessingServiceMock.Object);

        await service.ProcessOrder(order, unitOfWork);

        orderRepositoryMock.Verify(
            r => r.SetOrderSendConditionNotMetAsync(unitOfWork, order, OrderProcessingStatus.Retrying, It.IsAny<CancellationToken>()),
            Times.Once);
        emailOrderProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_WhenConditionCheckFailsAndOrderIsRetrying_ProcessesOrder()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Sms, OrderProcessingStatus.Retrying, new Uri("https://condition.test"));
        var smsResult = new SmsOrderProcessingResult([], null);

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock
            .Setup(c => c.CheckSendCondition(order.ConditionEndpoint!))
            .ReturnsAsync(new ConditionClientError { StatusCode = 500, Message = "failed" });

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.PersistProcessingResultAsync(unitOfWork, order, It.IsAny<EmailOrderProcessingResult>(), smsResult, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var smsOrderProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsOrderProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ReturnsAsync(smsResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            conditionClient: conditionClientMock.Object,
            smsOrderProcessingService: smsOrderProcessingServiceMock.Object);

        await service.ProcessOrder(order, unitOfWork);

        smsOrderProcessingServiceMock.Verify(s => s.ProcessOrder(order), Times.Once);
        orderRepositoryMock.Verify(
            r => r.SetOrderSendConditionNotMetAsync(It.IsAny<UnitOfWork>(), It.IsAny<NotificationOrder>(), It.IsAny<OrderProcessingStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_WhenProcessingServiceThrows_ExceptionIsRethrown()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Sms);

        var smsOrderProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsOrderProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ThrowsAsync(new Exception("failure"));

        var service = GetTestService(smsOrderProcessingService: smsOrderProcessingServiceMock.Object);

        await Assert.ThrowsAsync<Exception>(() => service.ProcessOrder(order, unitOfWork));
    }

    [Fact]
    public async Task PastDueOrdersBackgroundService_ExecuteAsync_NoTasksConfigured_DoesNotProcessOrders()
    {
        var orderProcessingServiceMock = new Mock<IOrderProcessingService>();
        var config = Options.Create(new NotificationConfig
        {
            PastDueOrdersTaskCount = 0,
            RetryOrdersTaskCount = 0
        });

        var service = new TestablePastDueOrdersBackgroundService(orderProcessingServiceMock.Object, config, Mock.Of<ILogger<PastDueOrdersBackgroundService>>());

        await service.ExecuteForTestAsync(TestContext.Current.CancellationToken);

        orderProcessingServiceMock.Verify(s => s.StartProcessingPastDueOrders(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PastDueOrdersBackgroundService_ExecuteAsync_ConfiguredTasks_StartsPastDueAndRetryLoops()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int pastDueCalls = 0;
        int retryCalls = 0;
        int totalCalls = 0;
        const int expectedCalls = 3;
        var allLoopsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var orderProcessingServiceMock = new Mock<IOrderProcessingService>();
        orderProcessingServiceMock
            .Setup(s => s.StartProcessingPastDueOrders(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns<bool, CancellationToken>(async (processRetry, _) =>
            {
                if (processRetry)
                {
                    Interlocked.Increment(ref retryCalls);
                }
                else
                {
                    Interlocked.Increment(ref pastDueCalls);
                }

                if (Interlocked.Increment(ref totalCalls) == expectedCalls)
                {
                    allLoopsStarted.TrySetResult();
                    cancellationTokenSource.Cancel();
                }

                await allLoopsStarted.Task;
                return true;
            });

        var config = Options.Create(new NotificationConfig
        {
            PastDueOrdersTaskCount = 2,
            RetryOrdersTaskCount = 1,
            PastDueOrdersIdleDelaySeconds = 0,
            RetryOrdersIdleDelaySeconds = 0
        });

        var service = new TestablePastDueOrdersBackgroundService(orderProcessingServiceMock.Object, config, Mock.Of<ILogger<PastDueOrdersBackgroundService>>());

        await service.ExecuteForTestAsync(cancellationTokenSource.Token);

        Assert.Equal(2, pastDueCalls);
        Assert.Equal(1, retryCalls);
    }

    [Fact]
    public async Task PastDueOrdersBackgroundService_ExecuteAsync_StartProcessingThrows_LogsAndRetries()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int calls = 0;
        var loggerMock = new Mock<ILogger<PastDueOrdersBackgroundService>>();

        var orderProcessingServiceMock = new Mock<IOrderProcessingService>();
        orderProcessingServiceMock
            .Setup(s => s.StartProcessingPastDueOrders(false, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("failure");
                }

                cancellationTokenSource.Cancel();
                return Task.FromResult(true);
            });

        var config = Options.Create(new NotificationConfig
        {
            PastDueOrdersTaskCount = 1,
            RetryOrdersTaskCount = 0,
            PastDueOrdersIdleDelaySeconds = 0,
            RetryOrdersIdleDelaySeconds = 0
        });

        var service = new TestablePastDueOrdersBackgroundService(orderProcessingServiceMock.Object, config, loggerMock.Object);

        await service.ExecuteForTestAsync(cancellationTokenSource.Token);

        Assert.Equal(2, calls);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Unhandled error in past due order loop.")),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static NotificationOrder CreateOrder(NotificationChannel notificationChannel, OrderProcessingStatus? orderProcessingStatus = null, Uri? conditionEndpoint = null)
    {
        return new NotificationOrder
        {
            Id = Guid.NewGuid(),
            NotificationChannel = notificationChannel,
            ConditionEndpoint = conditionEndpoint,
            OrderProcessingStatus = orderProcessingStatus
        };
    }

    private static OrderProcessingService GetTestService(
        IConditionClient? conditionClient = null,
        IOrderRepository? orderRepository = null,
        ISmsOrderProcessingService? smsOrderProcessingService = null,
        IEmailOrderProcessingService? emailOrderProcessingService = null,
        IPreferredChannelProcessingService? preferredChannelProcessingService = null,
        IEmailAndSmsOrderProcessingService? emailAndSmsOrderProcessingService = null,
        IUnitOfWorkRepository? unitOfWorkRepository = null)
    {
        if (orderRepository == null)
        {
            var orderRepositoryMock = new Mock<IOrderRepository>();
            orderRepository = orderRepositoryMock.Object;
        }

        if (smsOrderProcessingService == null)
        {
            var smsMockService = new Mock<ISmsOrderProcessingService>();
            smsOrderProcessingService = smsMockService.Object;
        }

        if (emailOrderProcessingService == null)
        {
            var emailMockService = new Mock<IEmailOrderProcessingService>();
            emailOrderProcessingService = emailMockService.Object;
        }

        if (preferredChannelProcessingService == null)
        {
            var preferredMockService = new Mock<IPreferredChannelProcessingService>();
            preferredChannelProcessingService = preferredMockService.Object;
        }

        if (conditionClient == null)
        {
            var conditionClientMock = new Mock<IConditionClient>();
            conditionClient = conditionClientMock.Object;
        }

        if (emailAndSmsOrderProcessingService == null)
        {
            var emailAndSmsProcessingService = new Mock<IEmailAndSmsOrderProcessingService>();
            emailAndSmsOrderProcessingService = emailAndSmsProcessingService.Object;
        }

        if (unitOfWorkRepository == null)
        {
            var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
            unitOfWorkRepository = unitOfWorkRepositoryMock.Object;
        }

        return new OrderProcessingService(
            orderRepository,
            emailOrderProcessingService,
            smsOrderProcessingService,
            preferredChannelProcessingService,
            emailAndSmsOrderProcessingService,
            conditionClient,
            new LoggerFactory().CreateLogger<OrderProcessingService>(),
            unitOfWorkRepository);
    }

    private sealed class TestablePastDueOrdersBackgroundService : PastDueOrdersBackgroundService
    {
        public TestablePastDueOrdersBackgroundService(
            IOrderProcessingService orderProcessingService,
            IOptions<NotificationConfig> config,
            ILogger<PastDueOrdersBackgroundService> logger)
            : base(orderProcessingService, config, logger)
        {
        }

        public Task ExecuteForTestAsync(CancellationToken stoppingToken)
        {
            return ExecuteAsync(stoppingToken);
        }
    }
}
