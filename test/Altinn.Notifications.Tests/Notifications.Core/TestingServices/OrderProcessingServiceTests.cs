using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class OrderProcessingServiceTests
{
    private readonly int _publishBatchSize = 50;
    private const string _pastDueTopicName = "orders.pastdue";

    [Fact]
    public async Task StartProcessingPastDueOrders_EmptyFirstFetch_ExitsImmediately()
    {
        // Arrange
        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var producerMock = new Mock<IKafkaProducer>();

        var orderProcessingService = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            producer: producerMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await orderProcessingService.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Once);
        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);
        producerMock.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var producerMock = new Mock<IKafkaProducer>();

        // Simulate producer failing to publish all messages: returns every serialized order as unpublished
        producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.Is<ImmutableList<string>>(msgs => msgs.Count == _publishBatchSize),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. batchOrders.Select(o => o.Serialize())]);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object, producer: producerMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await service.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Exactly(2));

        producerMock.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Once);

        foreach (var order in batchOrders)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Once);
        }
    }

    [Fact]
    public async Task StartProcessingPastDueOrders_CancellationRequestedAfterFirstFetch_Throws_AndRevertsProcessingStatus()
    {
        // Arrange
        var producerMock = new Mock<IKafkaProducer>();
        var orderRepositoryMock = new Mock<IOrderRepository>();

        var pastDueOrders = new List<NotificationOrder>
        {
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() }
        };

        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastDueOrders);

        var service = GetTestService(orderRepository: orderRepositoryMock.Object, producer: producerMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync(); // cancellation requested before method call

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartProcessingPastDueOrders(cancellationTokenSource.Token));

        orderRepositoryMock.Verify(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()), Times.Once);

        foreach (var order in pastDueOrders)
        {
            orderRepositoryMock.Verify(e => e.SetProcessingStatus(order.Id, OrderProcessingStatus.Registered), Times.Once);
        }

        producerMock.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var producerMock = new Mock<IKafkaProducer>();

        // First batch: all produced successfully
        producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.Is<ImmutableList<string>>(messages => messages.Count == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Second batch: simulate cancellation occurring mid-production (after 10 successes).
        var secondBatchSerialized = secondBatchOrders.Select(o => o.Serialize()).ToImmutableList();
        var unpublishedOrders = secondBatchSerialized.Skip(10).ToImmutableList(); // Skip first 10, return rest as unpublished

        using var cancellationTokenSource = new CancellationTokenSource();

        producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.Is<ImmutableList<string>>(messages => messages.Count == 25),
                It.IsAny<CancellationToken>()))
            .Callback<string, ImmutableList<string>, CancellationToken>((_, _, _) =>
            {
                cancellationTokenSource.Cancel();
            })
            .ReturnsAsync(unpublishedOrders);

        var orderProcessingService = GetTestService(
            orderRepository: orderRepositoryMock.Object,
            producer: producerMock.Object);

        // Act
        await orderProcessingService.StartProcessingPastDueOrders(cancellationTokenSource.Token);

        // Assert
        orderRepositoryMock.Verify(
            r => r.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()),
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

        var producerMock = new Mock<IKafkaProducer>();
        var service = GetTestService(orderRepository: orderRepositoryMock.Object, producer: producerMock.Object);

        using var cancellationTokenSource = new CancellationTokenSource();

        orderRepositoryMock
            .Setup(e => e.GetPastDueOrdersAndSetProcessingState(It.IsAny<CancellationToken>()))
            .Callback(cancellationTokenSource.Cancel)
            .ReturnsAsync(fetchedBatch);

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartProcessingPastDueOrders(cancellationTokenSource.Token));

        producerMock.Verify(e => e.ProduceAsync(It.IsAny<string>(), It.IsAny<ImmutableList<string>>(), It.IsAny<CancellationToken>()), Times.Never);

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
                notificationChannel: isEvenIndex ? NotificationChannel.Sms : NotificationChannel.Email));
        }

        return orders;
    }

    private static OrderProcessingService GetTestService(
        IKafkaProducer? producer = null,
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

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
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

        var kafkaSettings = new Altinn.Notifications.Core.Configuration.KafkaSettings() { PastDueOrdersTopicName = _pastDueTopicName };

        return new OrderProcessingService(orderRepository, emailOrderProcessingService, smsOrderProcessingService, preferredChannelProcessingService, emailAndSmsOrderProcessingService, conditionClient, producer, Options.Create(kafkaSettings), new LoggerFactory().CreateLogger<OrderProcessingService>());
    }
}
