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

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class EmailOrderProcessingServiceTests
{
    [Fact]
    public async Task ProcessOrder_NotificationServiceCalledOnceForEachRecipient()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Email,
            Recipients =
            [
                new()
                {
                    OrganizationNumber = "123456",
                    AddressInfo = [new EmailAddressPoint("email@test.com")]
                },
                new()
                {
                    OrganizationNumber = "654321",
                    AddressInfo = [new EmailAddressPoint("email@test.com")]
                }
            ]
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>())).ReturnsAsync([]);

        var service = GetTestService(emailService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessOrder_ExpectedInputToNotificationService()
    {
        // Arrange
        DateTime requested = DateTime.UtcNow;
        Guid orderId = Guid.NewGuid();

        var order = new NotificationOrder()
        {
            Id = orderId,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = requested,
            Recipients =
            [
                new([new EmailAddressPoint("test@test.com")], organizationNumber: "skd-orgno")
            ]
        };

        List<EmailAddressPoint> expectedEmailAddressPoints = [new("test@test.com")];
        EmailRecipient expectedEmailRecipient = new() { OrganizationNumber = "skd-orgno" };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(
            It.IsAny<Guid>(),
            It.Is<DateTime>(d => d.Equals(requested)),
            It.Is<List<EmailAddressPoint>>(r => AssertUtils.AreEquivalent(expectedEmailAddressPoints, r)),
            It.Is<EmailRecipient>(e => AssertUtils.AreEquivalent(expectedEmailRecipient, e)),
            It.IsAny<bool>())).ReturnsAsync([]);

        var service = GetTestService(emailService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrder_NotificationServiceThrowsException_PropagatesException()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            NotificationChannel = NotificationChannel.Email,
            Recipients =
            [
                new()
            ]
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception());

        var service = GetTestService(emailService: serviceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await service.ProcessOrder(order));

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_RecipientMissingEmail_ContactPointServiceCalled()
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
            Templates = [new EmailTemplate(null, "subject", "body", EmailContentType.Plain)]
        };

        var notificationServiceMock = new Mock<IEmailNotificationService>();
        notificationServiceMock.Setup(
            s => s.CreateNotification(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<List<EmailAddressPoint>>(),
                It.Is<EmailRecipient>(r => r.NationalIdentityNumber == "123456"),
                It.IsAny<bool>()))
            .ReturnsAsync([]);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock.Setup(c => c.AddEmailContactPoints(It.Is<List<Recipient>>(r => r.Count == 1), It.IsAny<string?>(), OrderLifecycleStage.Processing, It.IsAny<bool>(), It.IsAny<string?>()));

        var service = GetTestService(emailService: notificationServiceMock.Object, contactPointService: contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointServiceMock.Verify(c => c.AddEmailContactPoints(It.Is<List<Recipient>>(r => r.Count == 1), It.IsAny<string?>(), OrderLifecycleStage.Processing, It.IsAny<bool>(), It.IsAny<string?>()), Times.Once);
        notificationServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrderRetry_ServiceCalledForEachRecipient()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Email,
            Recipients =
            [
                new(),
                new([new EmailAddressPoint("test@test.com")], nationalIdentityNumber: "enduser-nin"),
                new([new EmailAddressPoint("test@test.com")], organizationNumber : "skd-orgNo"),
                new([new EmailAddressPoint("test@domain.com")])
            ]
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>())).ReturnsAsync([]);

        var service = GetTestService(emailService: serviceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()), Times.Exactly(order.Recipients.Count));
    }

    [Fact]
    public async Task ProcessOrder_WhenEmailSendingTimePolicyIsDaytime_ExpiryIsCalculatedBySchedulingService()
    {
        // Arrange
        var requestedSendTime = new DateTime(2025, 8, 25, 20, 0, 0, DateTimeKind.Utc);
        var daytimeExpiry = new DateTime(2025, 8, 28, 8, 0, 0, DateTimeKind.Utc);

        var order = CreateOrder(requestedSendTime, SendingTimePolicy.Daytime);

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        scheduleServiceMock.Setup(s => s.GetEmailExpirationDateTime(requestedSendTime)).Returns(daytimeExpiry);

        var service = GetTestService(emailService: CreateEmailServiceMock(), notificationScheduleService: scheduleServiceMock.Object);

        // Act
        var result = await service.ProcessOrder(order);

        // Assert
        Assert.Equal(daytimeExpiry, result.ExpirationDateTime);
        scheduleServiceMock.Verify(s => s.GetEmailExpirationDateTime(requestedSendTime), Times.Once);
    }

    [Theory]
    [InlineData(SendingTimePolicy.Anytime)]
    [InlineData(null)]
    public async Task ProcessOrder_WhenEmailSendingTimePolicyIsNotDaytime_ExpiryIs48HoursAfterRequestedSendTime(SendingTimePolicy? emailSendingTimePolicy)
    {
        // Arrange
        var requestedSendTime = new DateTime(2025, 8, 25, 20, 0, 0, DateTimeKind.Utc);

        var order = CreateOrder(requestedSendTime, emailSendingTimePolicy);

        var scheduleServiceMock = new Mock<INotificationScheduleService>();
        var service = GetTestService(emailService: CreateEmailServiceMock(), notificationScheduleService: scheduleServiceMock.Object);

        // Act
        var result = await service.ProcessOrder(order);

        // Assert
        Assert.Equal(requestedSendTime.AddHours(48), result.ExpirationDateTime);
        scheduleServiceMock.Verify(s => s.GetEmailExpirationDateTime(It.IsAny<DateTime>()), Times.Never);
    }

    private static IEmailNotificationService CreateEmailServiceMock()
    {
        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>())).ReturnsAsync([]);
        return serviceMock.Object;
    }

    private static NotificationOrder CreateOrder(DateTime requestedSendTime, SendingTimePolicy? emailSendingTimePolicy)
    {
        return new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            RequestedSendTime = requestedSendTime,
            NotificationChannel = NotificationChannel.Email,
            EmailSendingTimePolicy = emailSendingTimePolicy,
            Recipients = [new([new EmailAddressPoint("email@test.com")], organizationNumber: "123456")]
        };
    }

    private static EmailOrderProcessingService GetTestService(
         IEmailNotificationService? emailService = null,
         IContactPointService? contactPointService = null,
         IKeywordsService? keywordsService = null,
         INotificationScheduleService? notificationScheduleService = null)
    {
        if (emailService == null)
        {
            var emailServiceMock = new Mock<IEmailNotificationService>();
            emailService = emailServiceMock.Object;
        }

        if (contactPointService == null)
        {
            var contactPointServiceMock = new Mock<IContactPointService>();
            contactPointServiceMock
               .Setup(e => e.AddEmailContactPoints(It.IsAny<List<Recipient>>(), It.IsAny<string?>(), It.IsAny<OrderLifecycleStage>(), It.IsAny<bool>(), It.IsAny<string?>()));
            contactPointService = contactPointServiceMock.Object;
        }

        if (keywordsService == null)
        {
            var keywordsServiceMock = new Mock<IKeywordsService>();
            keywordsServiceMock.Setup(e => e.ReplaceKeywordsAsync(It.IsAny<IEnumerable<EmailRecipient>>())).ReturnsAsync((IEnumerable<EmailRecipient> recipient) => recipient);
            keywordsService = keywordsServiceMock.Object;
        }

        notificationScheduleService ??= new Mock<INotificationScheduleService>().Object;

        return new EmailOrderProcessingService(emailService, contactPointService, keywordsService, notificationScheduleService);
    }
}
