using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class OrderProcessingServiceTests
{
    private const string _pastDueTopicName = "orders.pastdue";

    [Fact]
    public async Task StartProcessingPastDueOrders_ProducerCalledOnceForEachOrder()
    {
        // Arrange 
        NotificationOrder order = TestdataUtil.GetOrderForTest();

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.GetPastDueOrdersAndSetProcessingState())
        .ReturnsAsync(new List<NotificationOrder>() { order, order, order, order });

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
    public async Task ProcessOrder_EmailOrder_EmailServiceCalled()
    {
        // Arrange 
        NotificationOrder order = TestdataUtil.GetOrderForTest(
            NotificationOrder
             .GetBuilder()
             .SetNotificationChannel(NotificationChannel.Email));

        var emailMockService = new Mock<IEmailOrderProcessingService>();
        emailMockService.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        smsMockService.Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(emailMock: emailMockService.Object, smsMock: smsMockService.Object);

        // Act
        await orderProcessingService.ProcessOrder(order);

        // Assert
        emailMockService.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);
        smsMockService.Verify(s => s.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SmsOrder_SmsServiceCalled()
    {
        // Arrange 
        NotificationOrder order = TestdataUtil.GetOrderForTest(
            NotificationOrder
              .GetBuilder()
              .SetNotificationChannel(NotificationChannel.Sms));

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        smsMockService.Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()));

        var emailMockService = new Mock<IEmailOrderProcessingService>();
        emailMockService.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(emailMock: emailMockService.Object, smsMock: smsMockService.Object);

        // Act
        await orderProcessingService.ProcessOrder(order);

        // Assert
        smsMockService.Verify(s => s.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);
        emailMockService.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SerivceThrowsException_ProcessingStatusIsNotSet()
    {
        // Arrange 
        NotificationOrder order = TestdataUtil.GetOrderForTest(
            NotificationOrder
                  .GetBuilder()
                  .SetNotificationChannel(NotificationChannel.Sms));

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        smsMockService.Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))));

        var orderProcessingService = GetTestService(repo: repoMock.Object, smsMock: smsMockService.Object);

        // Act      
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrder(order));

        // Assert
        smsMockService.Verify(s => s.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);
        repoMock.Verify(
                r => r.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))),
                Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_SmsOrder_SmsServiceCalled()
    {
        // Arrange 
        NotificationOrder order = TestdataUtil.GetOrderForTest(
            NotificationOrder
                .GetBuilder()
                .SetNotificationChannel(NotificationChannel.Sms));

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        smsMockService.Setup(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()));

        var emailMockService = new Mock<IEmailOrderProcessingService>();
        emailMockService.Setup(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(emailMock: emailMockService.Object, smsMock: smsMockService.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        smsMockService.Verify(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);
        emailMockService.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_SerivceThrowsException_ProcessingStatusIsNotSet()
    {
        // Arrange 
        NotificationOrder order = TestdataUtil.GetOrderForTest(
            NotificationOrder
                .GetBuilder()
                .SetId(Guid.NewGuid())
                .SetNotificationChannel(NotificationChannel.Sms));

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        smsMockService.Setup(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>())).Throws(new Exception());

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))));

        var orderProcessingService = GetTestService(repo: repoMock.Object, smsMock: smsMockService.Object);

        // Act      
        await Assert.ThrowsAsync<Exception>(async () => await orderProcessingService.ProcessOrderRetry(order));

        // Assert
        smsMockService.Verify(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);
        repoMock.Verify(
                r => r.SetProcessingStatus(It.IsAny<Guid>(), It.Is<OrderProcessingStatus>(s => s.Equals(OrderProcessingStatus.Completed))),
                Times.Never);
    }

    private static OrderProcessingService GetTestService(
        IOrderRepository? repo = null,
        IEmailOrderProcessingService? emailMock = null,
        ISmsOrderProcessingService? smsMock = null,
        IKafkaProducer? producer = null)
    {
        if (repo == null)
        {
            var repoMock = new Mock<IOrderRepository>();
            repo = repoMock.Object;
        }

        if (emailMock == null)
        {
            var emailMockService = new Mock<IEmailOrderProcessingService>();
            emailMock = emailMockService.Object;
        }

        if (smsMock == null)
        {
            var smsMockService = new Mock<ISmsOrderProcessingService>();
            smsMock = smsMockService.Object;
        }

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        var kafkaSettings = new Altinn.Notifications.Core.Configuration.KafkaSettings() { PastDueOrdersTopicName = _pastDueTopicName };

        return new OrderProcessingService(repo, emailMock, smsMock, producer, Options.Create(kafkaSettings));
    }
}
