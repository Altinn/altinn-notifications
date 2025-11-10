using System;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
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
    private const string _pastDueTopicName = "orders.pastdue";

    [Fact]
    public async Task StartProcessingPastDueOrders_ProducerIsCalledOnceForEachOrder()
    {
        // Arrange 
        NotificationOrder order = new();

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock.Setup(r => r.GetPastDueOrdersAndSetProcessingState()).ReturnsAsync([order, order, order, order]);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_pastDueTopicName)), It.IsAny<string>()));

        var service = GetTestService(orderRepository: orderRepositoryMock.Object, producer: producerMock.Object);

        // Act
        await service.StartProcessingPastDueOrders();

        // Assert
        orderRepositoryMock.Verify();
        producerMock.Verify(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_pastDueTopicName)), It.IsAny<string>()), Times.Exactly(4));

        orderRepositoryMock.Verify(e => e.InsertStatusFeedForOrder(It.IsAny<Guid>()), Times.Never);
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
