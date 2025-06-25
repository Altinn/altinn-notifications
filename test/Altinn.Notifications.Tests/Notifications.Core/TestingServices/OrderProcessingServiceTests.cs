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
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithoutCondition_CompletesSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: smsProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithoutCondition_CompletesSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: emailProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithoutCondition_CompletesSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var emailAndSmsOrderProcessingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: emailAndSmsOrderProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        emailAndSmsOrderProcessingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithoutCondition_CompletesSuccessfully(NotificationChannel notificationChannel)
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel
        };

        var conditionClientMock = new Mock<IConditionClient>();

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var preferredChannelProcessingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: preferredChannelProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Never);

        preferredChannelProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithMetSendingCondition_CompletesSuccessfully()
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

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: smsProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithMetSendingCondition_CompletesSuccessfully()
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

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: emailProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithMetSendingCondition_CompletesSuccessfully()
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

        var emailAndSmsOrderProcessingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: emailAndSmsOrderProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailAndSmsOrderProcessingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithMetSendingCondition_CompletesSuccessfully(NotificationChannel notificationChannel)
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

        var preferredChannelProcessingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: preferredChannelProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        preferredChannelProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithUnmetSendingCondition_OrderProcessingStops()
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

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: smsProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithUnmetSendingCondition_OrderProcessingStops()
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

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: emailProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithUnmetSendingCondition_OrderProcessingStops()
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

        var emailAndSmsOrderProcessingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: emailAndSmsOrderProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailAndSmsOrderProcessingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithUnmetSendingCondition_OrderProcessingStops(NotificationChannel notificationChannel)
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

        var preferredChannelProcessingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: preferredChannelProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        preferredChannelProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithInvalidConditionResult_OrderProcessingStops_RetryIsRequired()
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

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsOrderProcessingService: smsProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(c => c.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithInvalidConditionResult_OrderProcessingStops_RetryIsRequired()
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

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailOrderProcessingService: emailProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(c => c.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrderWithInvalidConditionResult_OrderProcessingStops_RetryIsRequired()
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

        var emailAndSmsOrderProcessingServiceMock = new Mock<IEmailAndSmsOrderProcessingService>();

        var orderProcessingService = GetTestService(emailAndSmsOrderProcessingService: emailAndSmsOrderProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(c => c.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailAndSmsOrderProcessingServiceMock.Verify(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Theory]
    [InlineData(NotificationChannel.SmsPreferred)]
    [InlineData(NotificationChannel.EmailPreferred)]
    public async Task ProcessOrder_PreferredChannelWithInvalidConditionResult_OrderProcessingStops_RetryIsRequired(NotificationChannel notificationChannel)
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

        var preferredChannelProcessingServiceMock = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(preferredChannelProcessingService: preferredChannelProcessingServiceMock.Object, orderRepository: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(c => c.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        preferredChannelProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
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
