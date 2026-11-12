using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

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

        var result = await service.TryProcessOrder(false, TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepositoryMock.Verify(u => u.StartUnitOfWork(), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_WhenOrderProcessingThrows_SetsRetryStatusAndRollsBack()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Sms);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsProcessingServiceMock
            .Setup(s => s.ProcessOrder(order))
            .ThrowsAsync(new InvalidOperationException("failure"));

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            smsOrderProcessingService: smsProcessingServiceMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        var result = await service.TryProcessOrder(false, TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepositoryMock.Verify(u => u.SaveUnitOfWork(unitOfWork, "after_read_with_lock"), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWorkToSavepoint(It.IsAny<UnitOfWork>(), It.IsAny<string>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        orderRepositoryMock.Verify(
            r => r.SetRetryStatus(It.IsAny<UnitOfWork>(), It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_WhenSetRetryStatusThrows_RollsBackAndReturnsFalse()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Sms);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        orderRepositoryMock
            .Setup(r => r.SetRetryStatus(unitOfWork, order.Id, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("retry status failure"));

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsProcessingServiceMock
            .Setup(s => s.ProcessOrder(order))
            .ThrowsAsync(new InvalidOperationException("processing failure"));

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            smsOrderProcessingService: smsProcessingServiceMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object);

        var result = await service.TryProcessOrder(false, TestContext.Current.CancellationToken);

        Assert.False(result);
        unitOfWorkRepositoryMock.Verify(u => u.SaveUnitOfWork(unitOfWork, "after_read_with_lock"), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWorkToSavepoint(It.IsAny<UnitOfWork>(), It.IsAny<string>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        orderRepositoryMock.Verify(
            r => r.SetRetryStatus(It.IsAny<UnitOfWork>(), It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
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

        var result = await service.TryProcessOrder(true, TestContext.Current.CancellationToken);

        Assert.True(result);
        orderRepositoryMock.Verify(r => r.GetNextPastDueOrder(unitOfWork, true, It.IsAny<CancellationToken>()), Times.Once);
        smsProcessingServiceMock.Verify(s => s.ProcessOrder(order), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        orderRepositoryMock.Verify(
            r => r.PersistProcessingResultAsync(unitOfWork, order, It.IsAny<EmailOrderProcessingResult>(), smsResult, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryProcessOrder_EmailOrder_PersistsEmailResultAndCommits(bool withConditionEndpoint)
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.Email, conditionEndpoint: withConditionEndpoint ? new Uri("https://condition.test") : null);
        var emailResult = new EmailOrderProcessingResult([], null);

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();
        emailProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ReturnsAsync(emailResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object,
            emailOrderProcessingService: emailProcessingServiceMock.Object);

        var result = await service.TryProcessOrder(false, TestContext.Current.CancellationToken);

        Assert.True(result);
        emailProcessingServiceMock.Verify(s => s.ProcessOrder(order), Times.Once);
        orderRepositoryMock.Verify(
            r => r.PersistProcessingResultAsync(unitOfWork, order, emailResult, It.IsAny<SmsOrderProcessingResult>(), It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
    }

    [Fact]
    public async Task TryProcessOrder_EmailAndSmsOrder_PersistsCombinedResultAndCommits()
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(NotificationChannel.EmailAndSms);
        var orderProcessingResult = new OrderProcessingResult(new EmailOrderProcessingResult([], null), new SmsOrderProcessingResult([], null));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var emailAndSmsOrderProcessingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();
        emailAndSmsOrderProcessingServiceMock.Setup(s => s.ProcessOrderAsync(order)).ReturnsAsync(orderProcessingResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object,
            emailAndSmsOrderProcessingService: emailAndSmsOrderProcessingServiceMock.Object);

        var result = await service.TryProcessOrder(false, TestContext.Current.CancellationToken);

        Assert.True(result);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
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
    public async Task TryProcessOrder_PreferredChannelOrder_PersistsCombinedResultAndCommits(NotificationChannel notificationChannel)
    {
        var unitOfWork = new UnitOfWork();
        var order = CreateOrder(notificationChannel);
        var orderProcessingResult = new OrderProcessingResult(new EmailOrderProcessingResult([], null), new SmsOrderProcessingResult([], null));

        var unitOfWorkRepositoryMock = new Mock<IUnitOfWorkRepository>();
        unitOfWorkRepositoryMock.Setup(u => u.StartUnitOfWork()).ReturnsAsync(unitOfWork);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(r => r.GetNextPastDueOrder(unitOfWork, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var preferredChannelProcessingServiceMock = new Mock<IPreferredChannelProcessingService>();
        preferredChannelProcessingServiceMock.Setup(s => s.ProcessOrder(order)).ReturnsAsync(orderProcessingResult);

        var service = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            unitOfWorkRepository: unitOfWorkRepositoryMock.Object,
            preferredChannelProcessingService: preferredChannelProcessingServiceMock.Object);

        var result = await service.TryProcessOrder(false, TestContext.Current.CancellationToken);

        Assert.True(result);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(unitOfWork), Times.Once);
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

        var result = await service.TryProcessOrder(false, TestContext.Current.CancellationToken);

        Assert.False(result);

        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWork(unitOfWork), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.CommitUnitOfWork(It.IsAny<UnitOfWork>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.SaveUnitOfWork(It.IsAny<UnitOfWork>(), It.IsAny<string>()), Times.Never);
        unitOfWorkRepositoryMock.Verify(u => u.RollbackUnitOfWorkToSavepoint(It.IsAny<UnitOfWork>(), It.IsAny<string>()), Times.Never);
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
        IOrderRepository? orderRepository = null,
        IUnitOfWorkRepository? unitOfWorkRepository = null,
        ISmsOrderProcessingService? smsOrderProcessingService = null,
        IEmailOrderProcessingService? emailOrderProcessingService = null,
        IPreferredChannelProcessingService? preferredChannelProcessingService = null,
        IEmailAndSmsOrderProcessingService? emailAndSmsOrderProcessingService = null)
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
            new LoggerFactory().CreateLogger<OrderProcessingService>(),
            smsOrderProcessingService,
            emailOrderProcessingService,
            preferredChannelProcessingService,
            emailAndSmsOrderProcessingService,
            unitOfWorkRepository);
    }
}
