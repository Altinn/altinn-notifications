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
    private static Guid _notificationOrderId = Guid.NewGuid();
    private static DateTime _requestedSendTime = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessOrder_ExpectedInputToService()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            ResourceId = null,
            Id = _notificationOrderId,
            ConditionEndpoint = null,
            IgnoreReservation = null,
            Created = _requestedSendTime,
            Creator = new Creator("ttd"),
            Type = OrderType.Notification,
            RequestedSendTime = _requestedSendTime,
            SendingTimePolicy = SendingTimePolicy.Daytime,
            NotificationChannel = NotificationChannel.Sms,
            Templates = [new SmsTemplate("Altinn", "this is the body")],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")], nationalIdentityNumber: "enduser-nin")
            ]
        };

        var notificationScheduleServiceMock = new Mock<INotificationScheduleService>();
        notificationScheduleServiceMock
            .Setup(e => e.GetSmsExpirationDateTime(_requestedSendTime))
            .Returns(_requestedSendTime.AddHours(48));

        var smsAddressPoints = new List<SmsAddressPoint> { new("+4799999999"), };

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(s => s.CreateNotification(
            It.Is<Guid>(e => e.Equals(_notificationOrderId)),
            It.Is<DateTime>(e => e.Equals(_requestedSendTime)),
            It.Is<DateTime>(e => e.Equals(_requestedSendTime.AddHours(48))),
            It.Is<List<SmsAddressPoint>>(e => AssertUtils.AreEquivalent(smsAddressPoints, e)),
            It.Is<SmsRecipient>(e => e.NationalIdentityNumber == "enduser-nin"),
            It.IsAny<int>(),
            It.IsAny<bool>()));

        var service = GetTestService(
            smsService: notificationServiceMock.Object,
            notificationScheduleService: notificationScheduleServiceMock.Object);

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
            Id = _notificationOrderId,
            NotificationChannel = NotificationChannel.Sms,
            Recipients =
            [
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
            ],
            Templates = [new SmsTemplate("Altinn", "this is the body")]
        };

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<List<SmsAddressPoint>>(), It.IsAny<SmsRecipient>(), It.IsAny<int>(), It.IsAny<bool>()));

        var service = GetTestService(smsService: notificationServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        notificationServiceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<List<SmsAddressPoint>>(), It.IsAny<SmsRecipient>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessOrder_RecipientMissingMobileNumber_ContactPointServiceCalled()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = _notificationOrderId,
            NotificationChannel = NotificationChannel.Sms,
            Recipients =
            [
                new()
                {
                    NationalIdentityNumber = "123456",
                }
            ],
            Templates = [new SmsTemplate("Altinn", "this is the body")]
        };

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(
            s => s.CreateNotification(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<List<SmsAddressPoint>>(),
                It.Is<SmsRecipient>(r => r.NationalIdentityNumber == "123456"),
                It.IsAny<int>(),
                It.IsAny<bool>()));

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock.Setup(c => c.AddSmsContactPoints(It.Is<List<Recipient>>(r => r.Count == 1), It.IsAny<string?>()))
            .Callback<List<Recipient>, string?>((r, _) =>
            {
                Recipient augumentedRecipient = new() { AddressInfo = [new SmsAddressPoint("+4712345678")], NationalIdentityNumber = r[0].NationalIdentityNumber };
                r.Clear();
                r.Add(augumentedRecipient);
            });

        var service = GetTestService(smsService: notificationServiceMock.Object, contactPointService: contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointServiceMock.Verify(c => c.AddSmsContactPoints(It.Is<List<Recipient>>(r => r.Count == 1), It.IsAny<string?>()), Times.Once);
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
            Recipients =
            [
                new Recipient(),
                new Recipient([new SmsAddressPoint("+4749999999")]),
                new Recipient([new SmsAddressPoint("+4799999999")], organizationNumber: "skd-orgNo"),
                new Recipient([new SmsAddressPoint("+4799999999")], nationalIdentityNumber: "enduser-nin")
            ],

            Templates = [new SmsTemplate("Altinn", "this is the body")]
        };

        var notificationServiceMock = new Mock<ISmsNotificationService>();
        notificationServiceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<List<SmsAddressPoint>>(), It.IsAny<SmsRecipient>(), It.IsAny<int>(), It.IsAny<bool>()));

        var smsRepoMock = new Mock<ISmsNotificationRepository>();
        smsRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(
        [
            new SmsRecipient() { OrganizationNumber = "skd-orgNo", MobileNumber = "+4799999999" },
            new SmsRecipient() { NationalIdentityNumber = "enduser-nin", MobileNumber = "+4799999999" }
        ]);

        var service = GetTestService(smsRepo: smsRepoMock.Object, smsService: notificationServiceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        smsRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
        notificationServiceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<List<SmsAddressPoint>>(), It.IsAny<SmsRecipient>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    private static SmsOrderProcessingService GetTestService(
        IKeywordsService? keywordsService = null,
        ISmsNotificationRepository? smsRepo = null,
        ISmsNotificationService? smsService = null,
        IContactPointService? contactPointService = null,
        INotificationScheduleService? notificationScheduleService = null)
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

        if (keywordsService == null)
        {
            var keywordsServiceMock = new Mock<IKeywordsService>();
            keywordsServiceMock.Setup(e => e.ReplaceKeywordsAsync(It.IsAny<List<SmsRecipient>>())).ReturnsAsync((List<SmsRecipient> recipient) => recipient);
            keywordsService = keywordsServiceMock.Object;
        }

        if (contactPointService == null)
        {
            var contactPointServiceMock = new Mock<IContactPointService>();
            contactPointServiceMock.Setup(e => e.AddSmsContactPoints(It.IsAny<List<Recipient>>(), It.IsAny<string?>()));

            contactPointService = contactPointServiceMock.Object;
        }

        if (notificationScheduleService == null)
        {
            var notificationScheduleServiceMock = new Mock<INotificationScheduleService>();
            notificationScheduleServiceMock.Setup(e => e.GetSmsExpirationDateTime(It.IsAny<DateTime>())).Returns((DateTime dt) => dt.AddHours(48));
            notificationScheduleService = notificationScheduleServiceMock.Object;
        }

        return new SmsOrderProcessingService(keywordsService, smsService, contactPointService, smsRepo, notificationScheduleService);
    }
}
