using System;
using System.Collections.Generic;
using System.Linq;
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
    public async Task ProcessOrderAsync_PersonRecipientMissingContactPoints_LooksUpAndProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var emailAddress = "recipient@altinn.xyz";
        var nationalIdentityNumber = "19230269672";

        var order = CreateNotificationOrderForSinglePerson(nationalIdentityNumber);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].NationalIdentityNumber == nationalIdentityNumber), It.Is<string>(resourceId => resourceId == order.ResourceId)))
            .Callback<List<Recipient>, string>((recipients, _) =>
            {
                recipients[0].AddressInfo.Add(new SmsAddressPoint(mobileNumber));
                recipients[0].AddressInfo.Add(new EmailAddressPoint(emailAddress));
            })
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber),
                order.ResourceId),
            Times.Once);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(ap => ap.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(ap => ap.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_SelfIdentifiedUserMissingContactPoints_LooksUpAndProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var emailAddress = "recipient@altinn.xyz";
        var externalIdentifier = $"urn:altinn:person:idporten-email:{emailAddress}";

        var order = CreateNotificationOrderForSingleSelfIdentifiedUser(externalIdentifier);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].ExternalIdentity == externalIdentifier), It.Is<string>(resourceId => resourceId == order.ResourceId)))
            .Callback<List<Recipient>, string>((recipients, _) =>
            {
                recipients[0].AddressInfo.Add(new SmsAddressPoint(mobileNumber));
                recipients[0].AddressInfo.Add(new EmailAddressPoint(emailAddress));
            })
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].ExternalIdentity == externalIdentifier),
                order.ResourceId),
            Times.Once);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].ExternalIdentity == externalIdentifier &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(s => s.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].ExternalIdentity == externalIdentifier &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(em => em.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_OrganizationRecipientMissingContactPoints_LooksUpAndProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var organizationNumber = "999888777";
        var emailAddress = "recipient@altinn.xyz";
        var order = CreateNotificationOrderForSingleOrganization(organizationNumber);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].OrganizationNumber == organizationNumber), It.Is<string>(resourceId => resourceId == order.ResourceId)))
            .Callback<List<Recipient>, string>((recipients, _) =>
            {
                recipients[0].AddressInfo.Add(new SmsAddressPoint(mobileNumber));
                recipients[0].AddressInfo.Add(new EmailAddressPoint(emailAddress));
            })
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber),
                order.ResourceId),
            Times.Once);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(ap => ap.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(ap => ap.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task ProcessOrderAsync_RecipientWithoutIdentifier_ThrowsArgumentException(bool isPersonRecipient, bool isOrganizationRecipient, bool isSelfIdentifiedRecipient)
    {
        // Arrange
        var order = (isPersonRecipient, isOrganizationRecipient, isSelfIdentifiedRecipient) switch
        {
            (true, _, _) => CreateNotificationOrderForSinglePerson(null),
            (_, true, _) => CreateNotificationOrderForSingleOrganization(null),
            _ => CreateNotificationOrderForSingleSelfIdentifiedUser(null)
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = new EmailAndSmsOrderProcessingService(
            new Mock<IEmailOrderProcessingService>().Object,
            new Mock<ISmsOrderProcessingService>().Object,
            contactPointServiceMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessOrderAsync(order));
    }

    [Fact]
    public async Task ProcessOrderRetryAsync_PersonRecipientMissingContactPoints_LooksUpAndProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var emailAddress = "recipient@altinn.xyz";
        var nationalIdentityNumber = "19230269672";

        var order = CreateNotificationOrderForSinglePerson(nationalIdentityNumber);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].NationalIdentityNumber == nationalIdentityNumber), It.Is<string>(resourceId => resourceId == order.ResourceId)))
            .Callback<List<Recipient>, string>((recipients, _) =>
            {
                recipients[0].AddressInfo.Add(new SmsAddressPoint(mobileNumber));
                recipients[0].AddressInfo.Add(new EmailAddressPoint(emailAddress));
            })
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetryAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber),
                order.ResourceId),
            Times.Once);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(ap => ap.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(ap => ap.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetryAsync_OrganizationRecipientMissingContactPoints_LooksUpAndProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var organizationNumber = "999888777";
        var emailAddress = "recipient@altinn.xyz";
        var order = CreateNotificationOrderForSingleOrganization(organizationNumber);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].OrganizationNumber == organizationNumber), It.Is<string>(resourceId => resourceId == order.ResourceId)))
            .Callback<List<Recipient>, string>((recipients, _) =>
            {
                recipients[0].AddressInfo.Add(new SmsAddressPoint(mobileNumber));
                recipients[0].AddressInfo.Add(new EmailAddressPoint(emailAddress));
            })
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetryAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber),
                order.ResourceId),
            Times.Once);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(ap => ap.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(ap => ap.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetryAsync_SelfIdentifiedUserRecipientMissingContactPoints_LooksUpAndProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var emailAddress = "recipient@altinn.xyz";
        var externalIdentifier = $"urn:altinn:person:idporten-email:{emailAddress}";

        var order = CreateNotificationOrderForSingleSelfIdentifiedUser(externalIdentifier);

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].ExternalIdentity == externalIdentifier), It.Is<string>(resourceId => resourceId == order.ResourceId)))
            .Callback<List<Recipient>, string>((recipients, _) =>
            {
                recipients[0].AddressInfo.Add(new SmsAddressPoint(mobileNumber));
                recipients[0].AddressInfo.Add(new EmailAddressPoint(emailAddress));
            })
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetryAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients => recipients.Count == 1 && recipients[0].ExternalIdentity == externalIdentifier),
                order.ResourceId),
            Times.Once);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].ExternalIdentity == externalIdentifier &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(s => s.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].ExternalIdentity == externalIdentifier &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(em => em.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetryAsync_PersonRecipientHoldingContactPoints_ProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var emailAddress = "recipient@altinn.xyz";
        var nationalIdentityNumber = "19230269672";

        var order = CreateNotificationOrder();
        order.Recipients =
        [
            new Recipient
            {
                IsReserved = false,
                ExternalIdentity = null,
                OrganizationNumber = null,
                NationalIdentityNumber = nationalIdentityNumber,
                AddressInfo = [new SmsAddressPoint(mobileNumber), new EmailAddressPoint(emailAddress)]
            }
        ];

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetryAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber),
                order.ResourceId),
            Times.Never);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(ap => ap.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].NationalIdentityNumber == nationalIdentityNumber &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(ap => ap.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetryAsync_OrganizationRecipientHoldingContactPoints_ProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var organizationNumber = "999888777";
        var emailAddress = "recipient@altinn.xyz";

        var order = CreateNotificationOrder();
        order.Recipients =
        [
            new Recipient
            {
                IsReserved = false,
                ExternalIdentity = null,
                NationalIdentityNumber = null,
                OrganizationNumber = organizationNumber,
                AddressInfo = [new SmsAddressPoint(mobileNumber), new EmailAddressPoint(emailAddress)]
            }
        ];

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetryAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber),
                order.ResourceId),
            Times.Never);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(ap => ap.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == organizationNumber &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(ap => ap.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetryAsync_SelfIdentifiedUserRecipientHoldingContactPoints_ProcessesBothChannels()
    {
        // Arrange
        var mobileNumber = "+4799999999";
        var emailAddress = "recipient@altinn.xyz";
        var externalIdentifier = $"urn:altinn:person:idporten-email:{emailAddress}";

        var order = CreateNotificationOrder();
        order.Recipients =
        [
            new Recipient
            {
                IsReserved = false,
                OrganizationNumber = null,
                NationalIdentityNumber = null,
                ExternalIdentity = externalIdentifier,
                AddressInfo = [new SmsAddressPoint(mobileNumber), new EmailAddressPoint(emailAddress)]
            }
        ];

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetryAsync(order);

        // Assert
        contactPointServiceMock.Verify(
            e => e.AddEmailAndSmsContactPointsAsync(
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].ExternalIdentity == externalIdentifier),
                order.ResourceId),
            Times.Never);

        smsProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == null &&
                    recipients[0].NationalIdentityNumber == null &&
                    recipients[0].ExternalIdentity == externalIdentifier &&
                    recipients[0].AddressInfo.OfType<SmsAddressPoint>().Any(ap => ap.MobileNumber == mobileNumber))),
            Times.Once);

        emailProcessingServiceMock.Verify(
            e => e.ProcessOrderRetryWithoutAddressLookup(
                It.Is<NotificationOrder>(o => o == order),
                It.Is<List<Recipient>>(recipients =>
                    recipients.Count == 1 &&
                    recipients[0].AddressInfo.Count == 1 &&
                    recipients[0].OrganizationNumber == null &&
                    recipients[0].NationalIdentityNumber == null &&
                    recipients[0].ExternalIdentity == externalIdentifier &&
                    recipients[0].AddressInfo.OfType<EmailAddressPoint>().Any(ap => ap.EmailAddress == emailAddress))),
            Times.Once);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task ProcessOrderRetryAsync_RecipientWithoutIdentifier_ThrowsArgumentException(bool isPersonRecipient, bool isOrganizationRecipient, bool isSelfIdentifiedRecipient)
    {
        // Arrange
        var order = (isPersonRecipient, isOrganizationRecipient, isSelfIdentifiedRecipient) switch
        {
            (true, _, _) => CreateNotificationOrderForSinglePerson(null),
            (_, true, _) => CreateNotificationOrderForSingleOrganization(null),
            _ => CreateNotificationOrderForSingleSelfIdentifiedUser(null)
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock
            .Setup(e => e.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = new EmailAndSmsOrderProcessingService(
            new Mock<IEmailOrderProcessingService>().Object,
            new Mock<ISmsOrderProcessingService>().Object,
            contactPointServiceMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessOrderRetryAsync(order));
    }

    /// <summary>
    /// Creates a <see cref="NotificationOrder"/> for test purposes.
    /// </summary>
    /// <returns>A new <see cref="NotificationOrder"/> instance.</returns>
    private static NotificationOrder CreateNotificationOrder()
    {
        return new NotificationOrder
        {
            Id = Guid.NewGuid(),
            Creator = new("ttd"),
            IgnoreReservation = true,
            ConditionEndpoint = null,
            Type = OrderType.Notification,
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
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.EmailAndSms,
            ResourceId = "urn:altinn:resource:ssb-correspondence-dev",
        };
    }

    /// <summary>
    /// Creates a new <see cref="NotificationOrder"/> for testing purposes with a single person recipient.
    /// </summary>
    /// <param name="nationalIdentityNumber">The national identity number of the recipient.</param>
    /// <returns>A new <see cref="NotificationOrder"/> instance.</returns>
    private static NotificationOrder CreateNotificationOrderForSinglePerson(string? nationalIdentityNumber)
    {
        var order = CreateNotificationOrder();

        order.Recipients =
        [
            new Recipient
            {
                AddressInfo = [],
                IsReserved = false,
                ExternalIdentity = null,
                OrganizationNumber = null,
                NationalIdentityNumber = nationalIdentityNumber
            }
        ];
        return order;
    }

    /// <summary>
    /// Creates a new <see cref="NotificationOrder"/> for testing purposes with a single organization recipient.
    /// </summary>
    /// <param name="organizationNumber">The organization number of the recipient.</param>
    /// <returns>A new <see cref="NotificationOrder"/> instance.</returns>
    private static NotificationOrder CreateNotificationOrderForSingleOrganization(string? organizationNumber)
    {
        var order = CreateNotificationOrder();

        order.Recipients =
        [
            new Recipient
            {
                AddressInfo = [],
                IsReserved = false,
                ExternalIdentity = null,
                NationalIdentityNumber = null,
                OrganizationNumber = organizationNumber
            }
        ];
        return order;
    }

    /// <summary>
    /// Creates a new <see cref="NotificationOrder"/> for testing purposes with a single self-identified user.
    /// </summary>
    /// <param name="externalIdentity">The external identity of the recipient.</param>
    /// <returns>A new <see cref="NotificationOrder"/> instance.</returns>
    private static NotificationOrder CreateNotificationOrderForSingleSelfIdentifiedUser(string? externalIdentity)
    {
        var order = CreateNotificationOrder();

        order.Recipients =
        [
            new Recipient
            {
                AddressInfo = [],
                IsReserved = false,
                OrganizationNumber = null,
                NationalIdentityNumber = null,
                ExternalIdentity = externalIdentity
            }
        ];
        return order;
    }
}
