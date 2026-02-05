using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class EmailAndSmsOrderProcessingServiceTests
{
    [Fact]
    public async Task ProcessOrder_RecipientWithoutIdentifier_ThrowsArgumentException()
    {
        // Arrange
        var order = GetTestNotificationOrderForSinglePerson(null);

        var service = new EmailAndSmsOrderProcessingService(
            new Mock<IEmailOrderProcessingService>().Object,
            new Mock<ISmsOrderProcessingService>().Object,
            new Mock<IContactPointService>().Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessOrderAsync(order));
    }

    [Fact]
    public async Task ProcessOrder_PersonRecipientWithoutContactPoints_AddsContactPointsAndProcesses()
    {
        // Arrange
        var order = GetTestNotificationOrderForSinglePerson("19230269672");

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.Is<string>(resourceId => resourceId == order.ResourceId)))
            .Callback<List<Recipient>, string>((recipients, _) =>
            {
                recipients[0].AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                recipients[0].AddressInfo.Add(new EmailAddressPoint("recipient@altinn.xyz"));
            });

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].NationalIdentityNumber == order.Recipients[0].NationalIdentityNumber),
                order.ResourceId),
            Times.Once);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].AddressInfo.Exists(e => e.AddressType == AddressType.Sms))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].AddressInfo.Exists(e => e.AddressType == AddressType.Email))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_PersonRecipientWithContactPoints_SkipsAddingContactPointsAndProcesses()
    {
        // Arrange
        var order = GetTestNotificationOrderForSinglePerson("28217843679");
        order.Recipients[0].AddressInfo = [new EmailAddressPoint("recipient@altinn.xyz"), new SmsAddressPoint("+4799999999")];

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderAsync(order);

        // Assert
        contactPointServiceMock.Verify(s => s.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>()), Times.Never);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].AddressInfo.Exists(e => e.AddressType == AddressType.Sms))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].AddressInfo.Exists(ap => ap.AddressType == AddressType.Email))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_OrganizationRecipientWithContactPoints_SkipsAddingContactPointsAndProcesses()
    {
        // Arrange
        var order = GetTestNotificationOrderForSingleOrganization("999888777");
        order.Recipients[0].AddressInfo = [new EmailAddressPoint("org@altinn.xyz")];

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderAsync(order);

        // Assert
        contactPointServiceMock.Verify(s => s.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>()), Times.Never);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].OrganizationNumber == "999888777" &&
                    recipients[0].AddressInfo.Count == 0)),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.IsAny<NotificationOrder>(),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].OrganizationNumber == "999888777" &&
                    recipients[0].AddressInfo.Count == 1)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_PersonRecipientWithoutContactPoints_AddsContactPointsAndProcesses()
    {
        // Arrange
        var order = GetTestNotificationOrderForSinglePerson("28217843679");
        order.Recipients[0].AddressInfo = [new EmailAddressPoint("recipient@altinn.xyz"), new SmsAddressPoint("+4799999999")];

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();
        var contactPointServiceMock = new Mock<IContactPointService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetryAsync(order);

        // Assert
        smsProcessingServiceMock.Verify(
            s => s.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.IsAny<List<Recipient>>()),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.IsAny<List<Recipient>>()),
            Times.Once);

        smsProcessingServiceMock.Verify(s => s.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()), Times.Never);
        emailProcessingServiceMock.Verify(e => e.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()), Times.Never);
    }

    /// <summary>
    /// Creates a new <see cref="NotificationOrder"/> for test purposes with a single person recipient.
    /// </summary>
    /// <param name="nationalIdentityNumber">The national identity number of the recipient.</param>
    /// <returns>A new <see cref="NotificationOrder"/> instance.</returns>
    private static NotificationOrder GetTestNotificationOrderForSinglePerson(string? nationalIdentityNumber)
    {
        var order = GetBaseTestNotificationOrder();

        order.Recipients =
        [
            new Recipient
            {
                AddressInfo = [],
                IsReserved = false,
                OrganizationNumber = null,
                NationalIdentityNumber = nationalIdentityNumber
            }
        ];
        return order;
    }

    /// <summary>
    /// Creates a new <see cref="NotificationOrder"/> for test purposes with a single organization recipient.
    /// </summary>
    /// <param name="organizationNumber">The organization number of the recipient.</param>
    /// <returns>A new <see cref="NotificationOrder"/> instance.</returns>
    private static NotificationOrder GetTestNotificationOrderForSingleOrganization(string organizationNumber)
    {
        var order = GetBaseTestNotificationOrder();

        order.Recipients =
        [
            new Recipient
            {
                AddressInfo = [],
                IsReserved = false,
                NationalIdentityNumber = null,
                OrganizationNumber = organizationNumber
            }
        ];
        return order;
    }

    /// <summary>
    /// Creates a base <see cref="NotificationOrder"/> for test purposes.
    /// </summary>
    /// <returns>A new <see cref="NotificationOrder"/> instance.</returns>
    private static NotificationOrder GetBaseTestNotificationOrder()
    {
        return new NotificationOrder
        {
            Id = Guid.NewGuid(),
            Creator = new("ttd"),
            IgnoreReservation = true,
            Templates =
            [
                new SmsTemplate()
                {
                    Body = "This is a test SMS body with placeholders: {{recipientName}}, {{recipientNumber}}"
                },
                new EmailTemplate()
                {
                    Subject = "Test Email Subject",
                    Body = "This is a test email body with placeholders: {{recipientName}}, {{recipientNumber}}"
                }
            ],
            RequestedSendTime = DateTime.UtcNow,
            Created = DateTime.UtcNow.AddDays(-1),
            SendersReference = "senders-reference",
            NotificationChannel = NotificationChannel.EmailAndSms,
            ResourceId = "urn:altinn:resource:ssb-correspondence-dev",
        };
    }
}
