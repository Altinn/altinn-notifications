using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class SmsOrderProcessingServiceTests
{
    [Fact]
    public async Task ProcessOrder_ExpectedInputToService()
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
                new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") }, nationalIdentityNumber: "enduser-nin")
            }
        };

        Recipient expectedRecipient = new(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") }, nationalIdentityNumber: "enduser-nin");

        var serviceMock = new Mock<ISmsNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.Is<DateTime>(d => d.Equals(requested)), It.Is<Recipient>(r => AssertUtils.AreEquivalent(expectedRecipient, r))));

        var service = GetTestService(smsService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrder_ServiceCalledOnceForEachRecipient()
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
    public async Task ProcessOrderRetry_ServiceCalledIfRecipientNotInDatabase()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                new Recipient(),
                new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") },  nationalIdentityNumber: "enduser-nin"),
                new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") },  organisationNumber: "skd-orgNo"),
                new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4749999999") })
            }
        };

        var serviceMock = new Mock<ISmsNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()));

        var smsRepoMock = new Mock<ISmsNotificationRepository>();
        smsRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(
            new List<SmsRecipient>()
            {
                new SmsRecipient() { NationalIdentityNumber = "enduser-nin", MobileNumber = "+4799999999" },
                new SmsRecipient() { OrganisationNumber = "skd-orgNo", MobileNumber = "+4799999999" }
            });

        var service = GetTestService(smsRepo: smsRepoMock.Object, smsService: serviceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        smsRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>()), Times.Exactly(2));
    }

    private static SmsOrderProcessingService GetTestService(
        ISmsNotificationRepository? smsRepo = null,
        ISmsNotificationService? smsService = null)
    {
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

        return new SmsOrderProcessingService(smsRepo, smsService);
    }
}
