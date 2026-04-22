using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class OrderProcessingServiceTests
{
    private readonly int _publishBatchSize = 50;

    [Fact]
    public async Task StartProcessingPastDueOrders_EmptyFirstFetch_ExitsImmediately()
    {
        // Arrange
        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var publisherMock = new Mock<IPastDueOrderPublisher>();

        var orderProcessingService = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            publisher: publisherMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await orderProcessingService.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Once);
        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);
        publisherMock.Verify(e => e.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_AllMessagesUnpublished_RevertsEntireBatchToRegistered()
    {
        // Arrange
        var batchOrders = CreateOrderBatch(_publishBatchSize, "all-unpublished");

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .SetupSequence(r => r.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchOrders)
            .ReturnsAsync([]);

        var publisherMock = new Mock<IPastDueOrderPublisher>();

        // Simulate publisher failing to publish all messages: returns every order as failed
        publisherMock
            .Setup(p => p.PublishAsync(
                It.Is<IReadOnlyList<NotificationOrder>>(orders => orders.Count == _publishBatchSize),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchOrders);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object, publisher: publisherMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await service.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Exactly(2));

        publisherMock.Verify(e => e.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Once);

        foreach (var order in batchOrders)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Once);
        }
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_CancellationBeforeSecondFetch_DoesNotRevertFirstBatch()
    {
        // Arrange
        var firstBatch = CreateOrderBatch(50, "first-batch");

        var repo = new Mock<IOrderRepository>();
        repo
            .SetupSequence(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstBatch) // Iteration 1 succeeds
            .ThrowsAsync(new OperationCanceledException("Canceled before second fetch")); // Iteration 2 throws before assignment

        var publisherMock = new Mock<IPastDueOrderPublisher>();
        publisherMock
            .Setup(e => e.PublishAsync(
                It.Is<IReadOnlyList<NotificationOrder>>(orders => orders.Count == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]); // All published successfully

        var service = GetTestService(orderRepository: repo.Object, publisher: publisherMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartProcessingPastDueOrders(cancellationTokenSource.Token));

        repo.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Exactly(2));

        publisherMock.Verify(e => e.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Once);

        foreach (var order in firstBatch)
        {
            repo.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Never);
        }
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_PartialFailures_MultipleValidFailedOrdersAreRevertedToRegistered()
    {
        // Arrange
        var batchOrders = CreateOrderBatch(_publishBatchSize, "partial-failures");
        var failedOrders = batchOrders.Where((_, idx) => idx is 3 or 7 or 11).ToList(); // choose 3 failed orders

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .SetupSequence(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchOrders)
            .ReturnsAsync([]); // stop loop

        var publisherMock = new Mock<IPastDueOrderPublisher>();

        // Publisher returns only selected failed orders (partial success)
        publisherMock
            .Setup(e => e.PublishAsync(
                It.Is<IReadOnlyList<NotificationOrder>>(orders => orders.Count == _publishBatchSize),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedOrders);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object, publisher: publisherMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await service.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Exactly(2));
        publisherMock.Verify(e => e.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Once);

        foreach (var order in failedOrders)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Once);
        }

        var succeededOrders = batchOrders.Except(failedOrders).ToList();
        foreach (var order in succeededOrders)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Never);
        }
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_CancellationRequestedAfterFirstFetch_Throws_AndRevertsProcessingStatus()
    {
        // Arrange
        var publisherMock = new Mock<IPastDueOrderPublisher>();
        var orderRepositoryMock = new Mock<IOrderRepository>();

        var pastDueOrders = new List<NotificationOrder>
        {
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() }
        };

        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastDueOrders);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object, publisher: publisherMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync(); // cancellation requested before method call

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartProcessingPastDueOrders(cancellationTokenSource.Token));

        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Once);

        foreach (var order in pastDueOrders)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Once);
        }

        publisherMock.Verify(e => e.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_SmallBatch_OnlyFailedOrdersAreReverted()
    {
        // Arrange
        var batchOrders = CreateOrderBatch(20, "small-batch");
        var failedOrders = batchOrders.Where((_, idx) => idx is 1 or 5 or 9).ToList();

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchOrders); // 20 < 50, loop exits after one iteration

        var publisherMock = new Mock<IPastDueOrderPublisher>();

        publisherMock
            .Setup(e => e.PublishAsync(
                It.Is<IReadOnlyList<NotificationOrder>>(orders => orders.Count == 20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedOrders);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object, publisher: publisherMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await service.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(e => e.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Once);

        foreach (var order in failedOrders)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Once);
        }

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.Is<Guid>(id => !failedOrders.Select(v => v.Id).Contains(id)), OrderProcessingStatus.Registered), Times.Never);
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_CancellationRequestedDuringSecondBatch_UnproducedOrdersRevertedToRegistered()
    {
        // Arrange
        var firstBatchOrders = CreateOrderBatch(_publishBatchSize, "first-batch");
        var secondBatchOrders = CreateOrderBatch(_publishBatchSize / 2, "second-batch");

        var orderRepositoryMock = new Mock<IOrderRepository>();

        orderRepositoryMock
            .SetupSequence(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstBatchOrders)
            .ReturnsAsync(secondBatchOrders)
            .ReturnsAsync([]);

        using var cancellationTokenSource = new CancellationTokenSource();

        var publisherMock = new Mock<IPastDueOrderPublisher>();

        // First batch: all published successfully
        publisherMock
            .Setup(p => p.PublishAsync(
                It.Is<IReadOnlyList<NotificationOrder>>(orders => orders.Count == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Second batch: simulate cancellation occurring mid-publish (after 10 successes).
        var unpublishedOrders = secondBatchOrders.Skip(10).ToList(); // Skip first 10, return rest as failed

        publisherMock
            .Setup(p => p.PublishAsync(
                It.Is<IReadOnlyList<NotificationOrder>>(orders => orders.Count == 25),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<NotificationOrder>, CancellationToken>((_, _) =>
            {
                cancellationTokenSource.Cancel();
            })
            .ReturnsAsync(unpublishedOrders);

        var orderProcessingService = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            publisher: publisherMock.Object);

        // Act
        await orderProcessingService.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(
            r => r.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        publisherMock.Verify(
            p => p.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        var unpublishedOrderIds = secondBatchOrders.Skip(10).Select(o => o.Id).ToList();
        foreach (var orderId in unpublishedOrderIds)
        {
            orderRepositoryMock.Verify(
                r => r.SetProcessingStatus(orderId, OrderProcessingStatus.Registered),
                Times.Once);
        }

        var publishedOrderIds = firstBatchOrders.Select(o => o.Id).Concat(secondBatchOrders.Take(10).Select(o => o.Id)).ToList();
        foreach (var orderId in publishedOrderIds)
        {
            orderRepositoryMock.Verify(
                r => r.SetProcessingStatus(orderId, OrderProcessingStatus.Registered),
                Times.Never);
        }
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_CancellationRequestedAfterFetchAndBeforeProduce_ThrowsAndRevertsEntireBatchToRegistered()
    {
        // Arrange
        var fetchedBatch = CreateOrderBatch(10, "cancel-after-fetch");

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fetchedBatch);

        var publisherMock = new Mock<IPastDueOrderPublisher>();
        var service = GetTestService(orderRepository: orderRepositoryMock.Object, publisher: publisherMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .Callback(cancellationTokenSource.Cancel)
            .ReturnsAsync(fetchedBatch);

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartProcessingPastDueOrders(cancellationTokenSource.Token));

        publisherMock.Verify(e => e.PublishAsync(It.IsAny<IReadOnlyList<NotificationOrder>>(), It.IsAny<CancellationToken>()), Times.Never);

        foreach (var order in fetchedBatch)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Once);
        }
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithoutCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithoutCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithoutCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        processingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithoutCondition_ProcessedSuccessfully(NotificationChannel notificationChannel)
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithMetSendingCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithMetSendingCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithMetSendingCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithMetSendingCondition_ProcessedSuccessfully(NotificationChannel notificationChannel)
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithUnmetSendingCondition_ProcessingStopped()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithUnmetSendingCondition_ProcessingStopped()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithUnmetSendingCondition_ProcessingStopped()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithUnmetSendingCondition_ProcessingStopped(NotificationChannel notificationChannel)
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithUnmetSendingCondition_InsertStatusFeedThrowsException_ProcessingContinues()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>())).ThrowsAsync(new Exception("Database error"));

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);
        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), OrderProcessingStatus.SendConditionNotMet), Times.Once);
        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);
        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithUnmetSendingCondition_InsertStatusFeedThrowsInvalidOperationException_ProcessingContinues()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("Order not found"));

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);
        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), OrderProcessingStatus.SendConditionNotMet), Times.Once);
        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);
        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithInvalidConditionResult_ProcessingStopped_RetryIsRequired()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithInvalidConditionResult_ProcessingStopped_RetryIsRequired()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithInvalidConditionResult_ProcessingStopped_RetryIsRequired()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithInvalidConditionResult_ProcessingStopped_RetryIsRequired(NotificationChannel notificationChannel)
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrder_ProcessingServiceThrowsException_ProcessingStopped()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();
        processingServiceMock.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, smsOrderProcessingService: processingServiceMock.Object);

        // Act      
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrder(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(e => e.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrder_ProcessingServiceThrowsException_ProcessingStopped()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();
        processingServiceMock.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, emailOrderProcessingService: processingServiceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrder(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(e => e.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrder_ProcessingServiceThrowsException_ProcessingStopped()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();
        processingServiceMock.Setup(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, emailAndSmsOrderProcessingService: processingServiceMock.Object);

        // Act      
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrder(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(e => e.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannel_ProcessingServiceThrowsException_ProcessingStopped(NotificationChannel notificationChannel)
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();
        processingServiceMock.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, preferredChannelProcessingService: processingServiceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrder(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(e => e.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_SmsOrderWithMetSendingCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailOrderWithMetSendingCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailAndSmsOrderWithMetSendingCondition_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetryAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrderRetry_PreferredChannelWithMetSendingCondition_ProcessedSuccessfully(NotificationChannel notificationChannel)
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_SmsOrderWithUnmetSendingCondition_ProcessingStopped()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailOrderWithUnmetSendingCondition_ProcessingStopped()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailAndSmsOrderWithUnmetSendingCondition_ProcessingStopped()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetryAsync(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrderRetry_PreferredChannelWithUnmetSendingCondition_ProcessingStopped(NotificationChannel notificationChannel)
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailOrderWithUnmetSendingCondition_InsertStatusFeedThrowsException_ProcessingContinues()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>())).ThrowsAsync(new Exception("Database error"));

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);
        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), OrderProcessingStatus.SendConditionNotMet), Times.Once);
        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);
        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailOrderWithUnmetSendingCondition_InsertStatusFeedThrowsInvalidOperationException_ProcessingContinues()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("Order not found"));

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);
        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), OrderProcessingStatus.SendConditionNotMet), Times.Once);
        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Once);
        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_SmsOrderWithInvalidConditionResult_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailOrderWithInvalidConditionResult_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailAndSmsOrderWithInvalidConditionResult_ProcessedSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetryAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrderRetry_PreferredChannelWithInvalidConditionResult_ProcessedSuccessfully(NotificationChannel notificationChannel)
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        var conditionClientMock = new Mock<IConditionClient>();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: processingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_SmsOrder_ProcessingServiceThrowsException_ProcessingStopped()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<ISmsOrderProcessingService>();
        processingServiceMock.Setup(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, smsOrderProcessingService: processingServiceMock.Object);

        // Act      
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrderRetry(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailOrder_ProcessingServiceThrowsException_ProcessingStopped()
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailOrderProcessingService>();
        processingServiceMock.Setup(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, emailOrderProcessingService: processingServiceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrderRetry(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailAndSmsOrder_ProcessingServiceThrowsException_ProcessingStopped()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();
        processingServiceMock.Setup(s => s.ProcessOrderRetryAsync(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, emailAndSmsOrderProcessingService: processingServiceMock.Object);

        // Act      
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrderRetry(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrderRetryAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrderRetry_PreferredChannel_ProcessingServiceThrowsException_ProcessingStopped(NotificationChannel notificationChannel)
    {
        // Arrange
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel
        };

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var processingServiceMock = new Mock<IPreferredChannelProcessingService>();
        processingServiceMock.Setup(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var orderProcessingService = GetTestService(orderRepository: orderRepositoryMock.Object, preferredChannelProcessingService: processingServiceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrderRetry(order));

        // Assert
        processingServiceMock.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))), Times.Never);
    }

    private static List<NotificationOrder> CreateOrderBatch(int count, string batchPrefix)
    {
        var orders = new List<NotificationOrder>();

        for (int i = 0; i < count; i++)
        {
            var isEvenIndex = i % 2 == 0;
            orders.Add(new(
                id: Guid.NewGuid(),
                type: OrderType.Notification,
                creator: new Creator("ttd"),
                created: DateTime.UtcNow.AddMinutes(-10),
                resourceId: "urn:altinn:resource:app_ttd_test",
                conditionEndpoint: null,
                ignoreReservation: false,
                sendersReference: $"{batchPrefix}-ref-{i:D3}",
                requestedSendTime: DateTime.UtcNow.AddMinutes(-5),
                recipients: [new Recipient([], nationalIdentityNumber: $"1234567890{i % 10}")],
                sendingTimePolicy: SendingTimePolicy.Daytime,
                templates: isEvenIndex ? [new SmsTemplate("TestSender", "Test SMS body")] : [new EmailTemplate("noreply@ttd.no", "Test Subject", "Test email body", EmailContentType.Plain)],
                notificationChannel: isEvenIndex ? NotificationChannel.Sms : NotificationChannel.Email,
                resourceAction: null));
        }

        return orders;
    }

    private static OrderProcessingService GetTestService(
        IPastDueOrderPublisher? publisher = null,
        IConditionClient? conditionClient = null,
        IOrderRepository? orderRepository = null,
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

        if (publisher == null)
        {
            var publisherMock = new Mock<IPastDueOrderPublisher>();
            publisher = publisherMock.Object;
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

        return new OrderProcessingService(orderRepository, emailOrderProcessingService, smsOrderProcessingService, preferredChannelProcessingService, emailAndSmsOrderProcessingService, conditionClient, publisher, new LoggerFactory().CreateLogger<OrderProcessingService>());
    }
}
