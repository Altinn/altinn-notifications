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

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.GetPastDueOrdersAndSetProcessingState()).ReturnsAsync([order, order, order, order]);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock.Setup(p => p.ProduceAsync(It.Is<string>(s => s.Equals(_pastDueTopicName)), It.IsAny<string>()));

        var service = GetTestService(repo: repoMock.Object, producer: producerMock.Object);

        // Act
        await service.StartProcessingPastDueOrders();

        // Assert
        repoMock.Verify();
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

        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()))
            .ReturnsAsync(true);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsProcessingServiceMock.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(smsMock: smsProcessingServiceMock.Object, repo: repoMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        repoMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithoutCondition_CompletesSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email
        };

        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()))
            .ReturnsAsync(true);

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();
        emailProcessingServiceMock.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(emailMock: emailProcessingServiceMock.Object, repo: repoMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        repoMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithTrueSendingCondition_CompletesSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        Mock<IConditionClient> conditionClientMock = new();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(repo => repo.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()))
            .ReturnsAsync(true);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        smsProcessingServiceMock.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(smsMock: smsProcessingServiceMock.Object, repo: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithTrueSendingCondition_CompletesSuccessfully()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        Mock<IConditionClient> conditionClientMock = new();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(true);

        var orderRepositoryMock = new Mock<IOrderRepository>();
        orderRepositoryMock
            .Setup(repo => repo.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()))
            .ReturnsAsync(true);

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();
        emailProcessingServiceMock.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(emailMock: emailProcessingServiceMock.Object, repo: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithFalseSendingCondition_SmsOrderProcessingStops()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        Mock<IConditionClient> conditionClientMock = new();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsMock: smsProcessingServiceMock.Object, repo: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithFalseSendingCondition_EmailOrderProcessingStops()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        Mock<IConditionClient> conditionClientMock = new();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(false);

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailMock: emailProcessingServiceMock.Object, repo: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.False(processingResult.IsRetryRequired);

        conditionClientMock.Verify(e => e.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrderWithInvalidConditionResult_RetryIsRequired_OrderProcessingStops()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        Mock<IConditionClient> conditionClientMock = new();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();

        var orderProcessingService = GetTestService(smsMock: smsProcessingServiceMock.Object, repo: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(c => c.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        smsProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_EmailOrderWithInvalidConditionResult_RetryIsRequired_OrderProcessingStops()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://sendingCondition.no")
        };

        Mock<IConditionClient> conditionClientMock = new();
        conditionClientMock.Setup(e => e.CheckSendCondition(It.Is<Uri>(e => e == order.ConditionEndpoint))).ReturnsAsync(new ConditionClientError());

        var orderRepositoryMock = new Mock<IOrderRepository>();

        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var orderProcessingService = GetTestService(emailMock: emailProcessingServiceMock.Object, repo: orderRepositoryMock.Object, conditionClient: conditionClientMock.Object);

        // Act
        var processingResult = await orderProcessingService.ProcessOrder(order);

        // Assert
        Assert.NotNull(processingResult);
        Assert.True(processingResult.IsRetryRequired);

        conditionClientMock.Verify(c => c.CheckSendCondition(It.IsAny<Uri>()), Times.Once);

        emailProcessingServiceMock.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);

        orderRepositoryMock.Verify(e => e.TryCompleteOrderBasedOnNotificationsState(It.IsAny<Guid>(), It.IsAny<AlternateIdentifierSource>()), Times.Never);
    }

    private static OrderProcessingService GetTestService(
        IOrderRepository? repo = null,
        IKafkaProducer? producer = null,
        IConditionClient? conditionClient = null,
        ISmsOrderProcessingService? smsMock = null,
        IEmailOrderProcessingService? emailMock = null,
        IPreferredChannelProcessingService? preferredMock = null,
        IEmailAndSmsOrderProcessingService? emailAndSmsMock = null)
    {
        if (repo == null)
        {
            var repoMock = new Mock<IOrderRepository>();
            repo = repoMock.Object;
        }

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        if (smsMock == null)
        {
            var smsMockService = new Mock<ISmsOrderProcessingService>();
            smsMock = smsMockService.Object;
        }

        if (emailMock == null)
        {
            var emailMockService = new Mock<IEmailOrderProcessingService>();
            emailMock = emailMockService.Object;
        }

        if (preferredMock == null)
        {
            var preferredMockService = new Mock<IPreferredChannelProcessingService>();
            preferredMock = preferredMockService.Object;
        }

        if (conditionClient == null)
        {
            var conditionClientMock = new Mock<IConditionClient>();
            conditionClient = conditionClientMock.Object;
        }

        if (emailAndSmsMock == null)
        {
            var emailAndSmsProcessingService = new Mock<IEmailAndSmsOrderProcessingService>();
            emailAndSmsMock = emailAndSmsProcessingService.Object;
        }

        var kafkaSettings = new Altinn.Notifications.Core.Configuration.KafkaSettings() { PastDueOrdersTopicName = _pastDueTopicName };

        return new OrderProcessingService(repo, emailMock, smsMock, preferredMock, emailAndSmsMock, conditionClient, producer, Options.Create(kafkaSettings), new LoggerFactory().CreateLogger<OrderProcessingService>());
    }
}
