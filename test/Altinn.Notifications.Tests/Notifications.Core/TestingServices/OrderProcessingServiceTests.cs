using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations.Interfaces;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;

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
    public async Task ProcessOrder_EmailNotificationChannel_ServiceCalledOnceForEachRecipient()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>()
            {
                new Recipient(),
                new Recipient()
            }
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()));

        var service = GetTestService(emailService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessOrder_EmailNotificationChannel_ExpectedInputToService()
    {
        // Arrange
        DateTime requested = DateTime.UtcNow;
        Guid orderId = Guid.NewGuid();

        var order = new NotificationOrder()
        {
            Id = orderId,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = requested,
            Recipients = new List<Recipient>()
            {
                new Recipient("skd", new List<IAddressPoint>() { new EmailAddressPoint("test@test.com") })
            }
        };

        Recipient expectedRecipient = new("skd", new List<IAddressPoint>() { new EmailAddressPoint("test@test.com") });

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.Is<DateTime>(d => d.Equals(requested)), It.Is<Recipient>(r => AssertUtils.AreEquivalent(expectedRecipient, r))));

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.Is<Guid>(s => s.Equals(orderId)), It.Is<OrderProcessingStatus>(s => s == OrderProcessingStatus.Completed)));

        var service = GetTestService(repo: repoMock.Object, emailService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.VerifyAll();
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrder_EmailNotificationChannel_ServiceThrowsException_RepositoryNotCalled()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>()
            {
                new Recipient()
            }
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()))
            .ThrowsAsync(new Exception());

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()));

        var service = GetTestService(repo: repoMock.Object, emailService: serviceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await service.ProcessOrder(order));

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Once);
        repoMock.Verify(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailNotificationChannel_ServiceCalledIfEmailNotificationNotCreated()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>()
            {
                new Recipient(),
                new Recipient("skd", new List<IAddressPoint>() { new EmailAddressPoint("test@test.com")})
            }
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()));

        var emailRepoMock = new Mock<IEmailNotificationRepository>();
        emailRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(new List<EmailRecipient>() { new EmailRecipient() { RecipientId = "skd", ToAddress = "test@test.com"}});

        var service = GetTestService(emailRepo: emailRepoMock.Object, emailService: serviceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Once);
        emailRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_EmailNotificationChannel_ServiceThrowsException_OrderRepositoryNotCalled()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>()
            {
                new Recipient()
            }
        };


        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()))
            .ThrowsAsync(new Exception());

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()));

        var emailRepoMock = new Mock<IEmailNotificationRepository>();
        emailRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(new List<EmailRecipient>() { new EmailRecipient() { RecipientId = "skd", ToAddress = "test@test.com"}});

        var service = GetTestService(repo: repoMock.Object, emailRepo: emailRepoMock.Object, emailService: serviceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await service.ProcessOrderRetry(order));

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Once);
        repoMock.Verify(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);
        emailRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
    }

    private static OrderProcessingService GetTestService(IOrderRepository? repo = null, IEmailNotificationRepository? emailRepo = null, IEmailNotificationService? emailService = null, IKafkaProducer? producer = null)
    {
        if (repo == null)
        {
            var _repo = new Mock<IOrderRepository>();
            repo = _repo.Object;
        }

        if (emailRepo == null)
        {
            var _emailRepo = new Mock<IEmailNotificationRepository>();
            emailRepo = _emailRepo.Object;
        }

        if (emailService == null)
        {
            var _emailService = new Mock<IEmailNotificationService>();
            emailService = _emailService.Object;
        }

        if (producer == null)
        {
            var _producer = new Mock<IKafkaProducer>();
            producer = _producer.Object;
        }

        var kafkaSettings = new KafkaSettings() { PastDueOrdersTopicName = _pastDueTopicName };

        return new OrderProcessingService(repo, emailRepo, emailService, producer, Options.Create(kafkaSettings));
    }
}