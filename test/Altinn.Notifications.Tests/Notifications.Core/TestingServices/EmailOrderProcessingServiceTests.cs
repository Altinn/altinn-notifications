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
            Recipients = new List<Recipient>()
            {
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
            }
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()));

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
        Guid orderId = Guid.NewGuid();
        DateTime requested = DateTime.UtcNow;

        var order = new NotificationOrder()
        {
            Id = orderId,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = requested,
            Recipients = new List<Recipient>()
            {
                new(new List<IAddressPoint>() { new EmailAddressPoint("test@test.com") }, organizationNumber: "skd-orgno")
            }
        };

        List<EmailAddressPoint> expectedEmailAddressPoints = [new("test@test.com")];
        EmailRecipient expectedEmailRecipient = new()
        {
            OrganizationNumber = "skd-orgno"
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.Is<DateTime>(d => d.Equals(requested)), It.Is<List<EmailAddressPoint>>(r => AssertUtils.AreEquivalent(expectedEmailAddressPoints, r)), It.Is<EmailRecipient>(e => AssertUtils.AreEquivalent(expectedEmailRecipient, e)), It.IsAny<bool>()));

        var service = GetTestService(emailService: serviceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrder_NotificationServiceThrowsException_RepositoryNotCalled()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>()
            {
                new()
            }
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception());

        var repoMock = new Mock<IOrderRepository>();
        repoMock.Setup(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()));

        var service = GetTestService(emailService: serviceMock.Object);

        // Act
        await Assert.ThrowsAsync<Exception>(async () => await service.ProcessOrder(order));

        // Assert
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()), Times.Once);
        repoMock.Verify(r => r.SetProcessingStatus(It.IsAny<Guid>(), It.IsAny<OrderProcessingStatus>()), Times.Never);
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
                It.IsAny<bool>()));

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock.Setup(c => c.AddEmailContactPoints(It.Is<List<Recipient>>(r => r.Count == 1), It.IsAny<string?>()));

        var service = GetTestService(emailService: notificationServiceMock.Object, contactPointService: contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointServiceMock.Verify(c => c.AddEmailContactPoints(It.Is<List<Recipient>>(r => r.Count == 1), It.IsAny<string?>()), Times.Once);
        notificationServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ProcessOrderRetry_ServiceCalledIfRecipientNotInDatabase()
    {
        // Arrange
        var order = new NotificationOrder()
        {
            Id = Guid.NewGuid(),
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>()
            {
                new(),
                new(new List<IAddressPoint>() { new EmailAddressPoint("test@test.com") }, nationalIdentityNumber: "enduser-nin"),
                new(new List<IAddressPoint>() { new EmailAddressPoint("test@test.com") }, organizationNumber : "skd-orgNo"),
                new(new List<IAddressPoint>() { new EmailAddressPoint("test@domain.com") })
            }
        };

        var serviceMock = new Mock<IEmailNotificationService>();
        serviceMock.Setup(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()));

        var emailRepoMock = new Mock<IEmailNotificationRepository>();
        emailRepoMock.Setup(e => e.GetRecipients(It.IsAny<Guid>())).ReturnsAsync(new List<EmailRecipient>()
        {
            new() { OrganizationNumber = "skd-orgNo", ToAddress = "test@test.com" },
            new() { NationalIdentityNumber = "enduser-nin", ToAddress = "test@test.com" }
        });

        var service = GetTestService(emailRepo: emailRepoMock.Object, emailService: serviceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        emailRepoMock.Verify(e => e.GetRecipients(It.IsAny<Guid>()), Times.Once);
        serviceMock.Verify(s => s.CreateNotification(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<List<EmailAddressPoint>>(), It.IsAny<EmailRecipient>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    private static EmailOrderProcessingService GetTestService(
        IEmailNotificationRepository? emailRepo = null,
        IEmailNotificationService? emailService = null,
        IContactPointService? contactPointService = null,
        IKeywordsService? keywordsService = null)
    {
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

        if (contactPointService == null)
        {
            var contactPointServiceMock = new Mock<IContactPointService>();
            contactPointServiceMock
               .Setup(e => e.AddEmailContactPoints(It.IsAny<List<Recipient>>(), It.IsAny<string?>()));
            contactPointService = contactPointServiceMock.Object;
        }

        if (keywordsService == null)
        {
            var keywordsServiceMock = new Mock<IKeywordsService>();
            keywordsServiceMock.Setup(e => e.ReplaceKeywordsAsync(It.IsAny<IEnumerable<EmailRecipient>>())).ReturnsAsync((IEnumerable<EmailRecipient> recipient) => recipient);
            keywordsService = keywordsServiceMock.Object;
        }

        return new EmailOrderProcessingService(emailRepo, emailService, contactPointService, keywordsService);
    }
}
