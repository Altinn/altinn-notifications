using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;

using RandomString4Net;

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
            },
            Templates = [new SmsTemplate("Altinn", "this is the body")]
        };

        Recipient expectedRecipient = new(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") }, nationalIdentityNumber: "enduser-nin");

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.Is<DateTime>(d => d.Equals(requested)), It.Is<Recipient>(r => AssertUtils.AreEquivalent(expectedRecipient, r)), It.IsAny<int>(), It.IsAny<bool>()));

        var service = GetTestService(smsService: notificationServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        notificationServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrder_NotificationServiceCalledOnceForEachRecipient()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                new()
                {
                OrganizationNumber = "123456",
                AddressInfo = [new SmsAddressPoint("+4799999999")]
                },
                new()
                {
                OrganizationNumber = "654321",
                AddressInfo = [new SmsAddressPoint("+4799999999")]
                }
            },
            Templates = [new SmsTemplate("Altinn", "this is the body")]
        };

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>(), It.IsAny<int>(), It.IsAny<bool>()));

        var service = GetTestService(smsService: notificationServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        notificationServiceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessOrder_RecipientMissingMobileNumber_ContactPointServiceCalled()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                new()
                {
                NationalIdentityNumber = "123456",
                }
            },
            Templates = [new SmsTemplate("Altinn", "this is the body")]
        };

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(
            s => s.CreateNotification(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.Is<Recipient>(r => r.NationalIdentityNumber == "123456"),
                It.IsAny<int>(),
                It.IsAny<bool>()));

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock.Setup(c => c.AddSmsContactPoints(It.Is<List<Recipient>>(r => r.Count == 1)))
            .Callback<List<Recipient>>(r =>
            {
                Recipient augumentedRecipient = new() { AddressInfo = [new SmsAddressPoint("+4712345678")], NationalIdentityNumber = r[0].NationalIdentityNumber };
                r.Clear();
                r.Add(augumentedRecipient);
            });

        var service = GetTestService(smsService: notificationServiceMock.Object, contactPointService: contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointServiceMock.Verify(c => c.AddSmsContactPoints(It.Is<List<Recipient>>(r => r.Count == 1)), Times.Once);
        notificationServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrderRetry_NotificationServiceCalledIfRecipientNotInDatabase()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                new Recipient(),
                new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") }, nationalIdentityNumber: "enduser-nin"),
                new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4799999999") }, organizationNumber: "skd-orgNo"),
                new Recipient(new List<IAddressPoint>() { new SmsAddressPoint("+4749999999") })
            },
            Templates = [new SmsTemplate("Altinn", "this is the body")]
        };

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>(), It.IsAny<int>(), It.IsAny<bool>()));

        var smsRepoMock = new Mock<ISmsNotificationRepository>();
        smsRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(
            [
                new SmsRecipient() { NationalIdentityNumber = "enduser-nin", MobileNumber = new("+4799999999") },
                new SmsRecipient() { OrganizationNumber = "skd-orgNo", MobileNumber = new("+4799999999") }
            ]);

        var service = GetTestService(smsRepo: smsRepoMock.Object, smsService: notificationServiceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        smsRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
        notificationServiceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<Recipient>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(160, 1)]
    [InlineData(161, 2)]
    [InlineData(18685, 16)]
    public void CalculateNumberOfMessages_LongMessagesAreSplitInMultiple(int messageLength, int expectedSmsCount)
    {
        int actualSmsCount = SmsOrderProcessingService.CalculateNumberOfMessages(RandomString.GetString(Types.ALPHABET_UPPERCASE, messageLength));
        Assert.Equal(expectedSmsCount, actualSmsCount);
    }

    [Fact]
    public void CalculateNumberOfMessages_MessageWithSymbolsAreEncodedBeforeCalculation()
    {
        int actualSmsCount = SmsOrderProcessingService.CalculateNumberOfMessages(RandomString.GetString(Types.ALPHABET_UPPERCASE_WITH_SYMBOLS, 160, forceOccuranceOfEachType: true));
        Assert.True(actualSmsCount > 1);
    }

    private static SmsOrderProcessingService GetTestService(
        ISmsNotificationRepository? smsRepo = null,
        ISmsNotificationService? smsService = null,
        IContactPointService? contactPointService = null)
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

        if (contactPointService == null)
        {
            var contactPointServiceMock = new Mock<IContactPointService>();
            contactPointServiceMock.Setup(e => e.AddSmsContactPoints(It.IsAny<List<Recipient>>()));

            contactPointService = contactPointServiceMock.Object;
        }

        return new SmsOrderProcessingService(smsRepo, smsService, contactPointService);
    }
}
