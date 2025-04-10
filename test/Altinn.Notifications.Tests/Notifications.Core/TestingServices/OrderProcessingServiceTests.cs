using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
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
    public async Task StartProcessingPastDueOrders_ProducerCalledOnceForEachOrder()
    {
        // Arrange 
        NotificationOrder order = new();

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
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Email,
        };

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
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
        };

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

    [Theory]
    [InlineData(NotificationChannel.EmailPreferred)]
    [InlineData(NotificationChannel.SmsPreferred)]
    public async Task ProcessOrder_EmailOrSmsPreferredOrder_PreferredServiceCalled(NotificationChannel notificationChannel)
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
        };

        var emailMockService = new Mock<IEmailOrderProcessingService>();
        emailMockService.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        smsMockService.Setup(s => s.ProcessOrder(It.IsAny<NotificationOrder>()));

        var preferredMockService = new Mock<IPreferredChannelProcessingService>();
        preferredMockService.Setup(e => e.ProcessOrder(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(emailMock: emailMockService.Object, smsMock: smsMockService.Object, preferredMock: preferredMockService.Object);

        // Act
        await orderProcessingService.ProcessOrder(order);

        // Assert
        emailMockService.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
        smsMockService.Verify(s => s.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
        preferredMockService.Verify(s => s.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_EmailAndSmsOrder_EmailAndSmsOrderProcessingServiceCalled()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms
        };

        var emailAndSmsMockService = new Mock<IEmailAndSmsOrderProcessingService>();
        emailAndSmsMockService.Setup(e => e.ProcessOrderAsync(It.IsAny<NotificationOrder>())).Returns(Task.CompletedTask);

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        var emailMockService = new Mock<IEmailOrderProcessingService>();
        var preferredChannelMockService = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(emailMock: emailMockService.Object, smsMock: smsMockService.Object, bothMock: emailAndSmsMockService.Object, preferredMock: preferredChannelMockService.Object);

        // Act
        await orderProcessingService.ProcessOrder(order);

        // Assert
        emailAndSmsMockService.Verify(e => e.ProcessOrderAsync(order), Times.Once);

        // Individual email and SMS services should not be called
        smsMockService.Verify(s => s.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
        emailMockService.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
        preferredChannelMockService.Verify(e => e.ProcessOrder(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrder_SendConditionNotMet_ProcessingStops()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://vg.no")
        };

        Mock<IConditionClient> clientMock = new();
        clientMock.Setup(c => c.CheckSendCondition(It.IsAny<Uri>())).ReturnsAsync(false);

        Mock<IOrderRepository> repoMock = new();
        repoMock.Setup(r => r.SetProcessingStatus(
            It.Is<Guid>(g => g.Equals(order.Id)),
            It.Is<OrderProcessingStatus>(ops => ops == OrderProcessingStatus.SendConditionNotMet)));
        var orderProcessingService = GetTestService(conditionClient: clientMock.Object, repo: repoMock.Object);

        // Act
        await orderProcessingService.ProcessOrder(order);

        // Assert
        clientMock.Verify(
            c => c.CheckSendCondition(It.IsAny<Uri>()),
            Times.Once);

        repoMock.Verify(
            r => r.SetProcessingStatus(It.Is<Guid>(g => g.Equals(order.Id)), It.Is<OrderProcessingStatus>(ops => ops == OrderProcessingStatus.SendConditionNotMet)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_SerivceThrowsException_ProcessingStatusIsNotSet()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
        };

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
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
        };

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

    [Theory]
    [InlineData(NotificationChannel.EmailPreferred)]
    [InlineData(NotificationChannel.SmsPreferred)]
    public async Task ProcessOrderRetry_EmailOrSmsPreferredOrder_PreferredServiceCalled(NotificationChannel notificationChannel)
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = notificationChannel,
        };
        var emailMockService = new Mock<IEmailOrderProcessingService>();
        emailMockService.Setup(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()));

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        smsMockService.Setup(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()));

        var preferredMockService = new Mock<IPreferredChannelProcessingService>();
        preferredMockService.Setup(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()));

        var orderProcessingService = GetTestService(emailMock: emailMockService.Object, smsMock: smsMockService.Object, preferredMock: preferredMockService.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        smsMockService.Verify(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
        emailMockService.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
        preferredMockService.Verify(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailAndSmsOrder_EmailAndSmsOrderProcessingServiceCalled()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.EmailAndSms,
        };

        var emailAndSmsMockService = new Mock<IEmailAndSmsOrderProcessingService>();
        emailAndSmsMockService.Setup(e => e.ProcessOrderRetryAsync(It.IsAny<NotificationOrder>())).Returns(Task.CompletedTask);

        var smsMockService = new Mock<ISmsOrderProcessingService>();
        var emailMockService = new Mock<IEmailOrderProcessingService>();
        var preferredChannelMockService = new Mock<IPreferredChannelProcessingService>();

        var orderProcessingService = GetTestService(emailMock: emailMockService.Object, smsMock: smsMockService.Object, bothMock: emailAndSmsMockService.Object, preferredMock: preferredChannelMockService.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        emailAndSmsMockService.Verify(e => e.ProcessOrderRetryAsync(order), Times.Once);

        // Individual email and SMS services should not be called
        smsMockService.Verify(s => s.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
        emailMockService.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
        preferredChannelMockService.Verify(e => e.ProcessOrderRetry(It.IsAny<NotificationOrder>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_SendConditionNotMet_ProcessingStops()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            ConditionEndpoint = new Uri("https://vg.no")
        };

        Mock<IConditionClient> clientMock = new();
        clientMock.Setup(c => c.CheckSendCondition(It.IsAny<Uri>())).ReturnsAsync(false);

        Mock<IOrderRepository> repoMock = new();
        repoMock.Setup(r => r.SetProcessingStatus(
            It.Is<Guid>(g => g.Equals(order.Id)),
            It.Is<OrderProcessingStatus>(ops => ops == OrderProcessingStatus.SendConditionNotMet)));
        var orderProcessingService = GetTestService(conditionClient: clientMock.Object, repo: repoMock.Object);

        // Act
        await orderProcessingService.ProcessOrderRetry(order);

        // Assert
        clientMock.Verify(
            c => c.CheckSendCondition(It.IsAny<Uri>()),
            Times.Once);

        repoMock.Verify(
            r => r.SetProcessingStatus(It.Is<Guid>(g => g.Equals(order.Id)), It.Is<OrderProcessingStatus>(ops => ops == OrderProcessingStatus.SendConditionNotMet)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_SerivceThrowsException_ProcessingStatusIsNotSet()
    {
        // Arrange 
        NotificationOrder order = new()
        {
            NotificationChannel = NotificationChannel.Sms,
        };

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

    [Fact]
    public async Task IsSendConditionMet_NoConditionEndpoint_ReturnsTrue()
    {
        // Arrange
        NotificationOrder order = new() { Id = Guid.NewGuid(), ConditionEndpoint = null };
        var orderProcessingService = GetTestService();

        // Act
        bool actual = await orderProcessingService.IsSendConditionMet(order, isRetry: false);

        // Assert
        Assert.True(actual);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task IsSendConditionMet_SuccessResultFromClient_ReturnsSameAsClient(bool expectedConditionResult, bool isRetry)
    {
        // Arrange
        NotificationOrder order = new() { Id = Guid.NewGuid(), ConditionEndpoint = new Uri("https://vg.no") };

        Mock<IConditionClient> clientMock = new();
        clientMock.Setup(c => c.CheckSendCondition(It.IsAny<Uri>())).ReturnsAsync(expectedConditionResult);
        var orderProcessingService = GetTestService(conditionClient: clientMock.Object);

        // Act
        bool actual = await orderProcessingService.IsSendConditionMet(order, isRetry: isRetry);

        // Assert
        Assert.Equal(expectedConditionResult, actual);
    }

    [Fact]
    public async Task IsSendConditionMet_ErrorResultOnFirstAttempt_ThrowsException()
    {
        // Arrange
        NotificationOrder order = new() { Id = Guid.NewGuid(), ConditionEndpoint = new Uri("https://vg.no") };

        Mock<IConditionClient> clientMock = new();
        clientMock.Setup(c => c.CheckSendCondition(It.IsAny<Uri>())).ReturnsAsync(new ConditionClientError());
        var orderProcessingService = GetTestService(conditionClient: clientMock.Object);

        // Act
        await Assert.ThrowsAsync<OrderProcessingException>(async () => await orderProcessingService.IsSendConditionMet(order, isRetry: false));
    }

    [Fact]
    public async Task IsSendConditionMet_ErrorResultOnRetry_ReturnsTrue()
    {
        // Arrange
        NotificationOrder order = new() { Id = Guid.NewGuid(), ConditionEndpoint = new Uri("https://vg.no") };

        Mock<IConditionClient> clientMock = new();
        clientMock.Setup(c => c.CheckSendCondition(It.IsAny<Uri>())).ReturnsAsync(new ConditionClientError());
        var orderProcessingService = GetTestService(conditionClient: clientMock.Object);

        // Act
        bool actual = await orderProcessingService.IsSendConditionMet(order, isRetry: true);

        // Assert
        Assert.True(actual);
    }

    private static OrderProcessingService GetTestService(
        IOrderRepository? repo = null,
        IEmailOrderProcessingService? emailMock = null,
        ISmsOrderProcessingService? smsMock = null,
        IEmailAndSmsOrderProcessingService? bothMock = null,
        IPreferredChannelProcessingService? preferredMock = null,
        IKafkaProducer? producer = null,
        IConditionClient? conditionClient = null)
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

        if (bothMock == null)
        {
            var emailAndSmsProcessingService = new Mock<IEmailAndSmsOrderProcessingService>();
            bothMock = emailAndSmsProcessingService.Object;
        }

        if (preferredMock == null)
        {
            var preferredMockService = new Mock<IPreferredChannelProcessingService>();
            preferredMock = preferredMockService.Object;
        }

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        if (conditionClient == null)
        {
            var conditionClientMock = new Mock<IConditionClient>();
            conditionClient = conditionClientMock.Object;
        }

        var kafkaSettings = new Altinn.Notifications.Core.Configuration.KafkaSettings() { PastDueOrdersTopicName = _pastDueTopicName };

        return new OrderProcessingService(repo, emailMock, smsMock, preferredMock, bothMock, conditionClient, producer, Options.Create(kafkaSettings), new LoggerFactory().CreateLogger<OrderProcessingService>());
    }
}
