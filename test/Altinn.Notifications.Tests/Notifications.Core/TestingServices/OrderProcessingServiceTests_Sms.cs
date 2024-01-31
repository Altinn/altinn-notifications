using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
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

public class OrderProcessingServiceTests_Sms
{
    [Fact]
    public async Task ProcessOrder_SmsNotificationChannel_ExpectedInputToService()
    {
        // Arrange
        DateTime requested = DateTime.UtcNow;
        Guid orderId = Guid.NewGuid();

        var order = new NotificationOrder()
        {
            Id = orderId,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = requested,
            Recipients = new List<Recipient>()
            {
                new Recipient("end-user", new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") })
            }
        };

        Recipient expectedRecipient = new("end-user", new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") });

        var serviceMock = new Mock<ISmsNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.Is<DateTime>(d => d.Equals(requested)), It.Is<Recipient>(r => AssertUtils.AreEquivalent(expectedRecipient, r))));

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.Is<Guid>(s => s.Equals(orderId)), It.Is<OrderProcessingStatus>(s => s == OrderProcessingStatus.Completed)));

        var service = GetTestService(repo: repoMock.Object, smsService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.VerifyAll();
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrder_SmsNotificationChannel_ServiceCalledOnceForEachRecipient()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                new(),
                new()
            }
        };

        var serviceMock = new Mock<ISmsNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()));

        var service = GetTestService(smsService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessOrder_SmsNotificationChannel_ServiceThrowsException_RepositoryNotCalled()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                new Recipient()
            }
        };

        var serviceMock = new Mock<ISmsNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()))
            .ThrowsAsync(new Exception());

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()));

        var service = GetTestService(repo: repoMock.Object, smsService: serviceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await service.ProcessOrder(order));

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Once);
        repoMock.Verify(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderRetry_SmsNotificationChannel_ServiceCalledIfSmsNotificationNotCreated()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                new Recipient(),
                new Recipient("end-user", new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") })
            }
        };

        var serviceMock = new Mock<ISmsNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()));

        var smsRepoMock = new Mock<ISmsNotificationRepository>();
        smsRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(new List<SmsRecipient>() { new SmsRecipient() { RecipientId = "end-user", MobileNumber = "+4799999999" } });

        var service = GetTestService(smsRepo: smsRepoMock.Object, smsService: serviceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        smsRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Once);
    }

    private static OrderProcessingService GetTestService(
        IOrderRepository? repo = null,
        ISmsNotificationRepository? smsRepo = null,
        ISmsNotificationService? smsService = null,
        IKafkaProducer? producer = null)
    {
        if (repo == null)
        {
            var repoMock = new Mock<IOrderRepository>();
            repo = repoMock.Object;
        }

        if (smsRepo == null)
        {
            var smsRepoMock = new Mock<ISmsNotificationRepository>();
            smsRepo = smsRepoMock.Object;
        }

        if (smsService == null)
        {
            var smsServiceMock = new Mock<ISmsNotificationService>();
            smsService = smsServiceMock.Object;
        }

        if (producer == null)
        {
            var producerMock = new Mock<IKafkaProducer>();
            producer = producerMock.Object;
        }

        var emailRepoMock = new Mock<IEmailNotificationRepository>();
        var emailServiceMock = new Mock<IEmailNotificationService>();
        return new OrderProcessingService(repo, emailRepoMock.Object, emailServiceMock.Object, smsRepo, smsService, producer, Options.Create(new KafkaSettings()));
    }
}
