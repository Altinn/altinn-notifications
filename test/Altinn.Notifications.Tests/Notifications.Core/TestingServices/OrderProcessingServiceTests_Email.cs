using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class OrderProcessingServiceTests_Email
{
    private const string _pastDueTopicName = "orders.pastdue";

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
                new(),
                new()
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
                new Recipient("skd", new List<IAddressPoint>() { new EmailAddressPoint("test@test.com") }),
                new Recipient(new List<IAddressPoint>() { new EmailAddressPoint("test@domain.com") })
            }
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()));

        var emailRepoMock = new Mock<IEmailNotificationRepository>();
        emailRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(new List<EmailRecipient>() { new EmailRecipient() { RecipientId = "skd", ToAddress = "test@test.com" } });

        var service = GetTestService(emailRepo: emailRepoMock.Object, emailService: serviceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Exactly(2));
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
        emailRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(new List<EmailRecipient>() { new EmailRecipient() { RecipientId = "skd", ToAddress = "test@test.com" } });

        var service = GetTestService(repo: repoMock.Object, emailRepo: emailRepoMock.Object, emailService: serviceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await service.ProcessOrderRetry(order));

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Once);
        repoMock.Verify(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);
        emailRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
    }

    private static OrderProcessingService GetTestService(
        IOrderRepository? repo = null,
        IEmailNotificationRepository? emailRepo = null,
        IEmailNotificationService? emailService = null,
        IKafkaProducer? producer = null)
    {
        if (repo == null)
        {
            var repoMock = new Mock<IOrderRepository>();
            repo = repoMock.Object;
        }

        if (emailRepo == null)
        {
            var emailRepoMock = new Mock<IEmailNotificationRepository>();
            emailRepo = emailRepoMock.Object;
        }

        if (emailService == null)
        {
            var emailServiceMock = new Mock<IEmailNotificationService>();
            emailService = emailServiceMock.Object;
        }

        var smsRepoMock = new Mock<ISmsNotificationRepository>();
        var smsServiceMock = new Mock<ISmsNotificationService>();

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        var kafkaSettings = new Altinn.Notifications.Core.Configuration.KafkaSettings() { PastDueOrdersTopicName = _pastDueTopicName };

        return new OrderProcessingService(repo, emailRepo, emailService, smsRepoMock.Object, smsServiceMock.Object, producer, Options.Create(kafkaSettings));
    }
}
