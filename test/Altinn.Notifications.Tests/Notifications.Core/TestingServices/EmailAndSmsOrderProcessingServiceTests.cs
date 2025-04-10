using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class EmailAndSmsOrderProcessingServiceTests
{
    [Fact]
    public async Task ProcessOrder_RecipientsWithoutContactPoints_CallsContactPointService()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients =
            [
                new Recipient
                {
                    AddressInfo = [],
                    NationalIdentityNumber = "19230269672",
                }
            ],
            ResourceId = "urn:altinn:resource"
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointServiceMock.Verify(e => e.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(r => r.Count == 1 && r[0].NationalIdentityNumber == "19230269672"), "urn:altinn:resource"), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_RecipientsWithExistingContactPoints_SkipsContactPointLookup()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients =
            [
                new Recipient
                {
                    NationalIdentityNumber = "28217843679",
                    AddressInfo = [new EmailAddressPoint("recipient@example.com")]
                }
            ],
            ResourceId = "urn:altinn:resource"
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        contactPointServiceMock.Verify(s => s.AddEmailAndSmsContactPointsAsync(It.Is<List<Recipient>>(r => r.Count == 0), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_WithSmsAndEmailRecipients_CallsRespectiveServices()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients =
            [
                new Recipient
                {
                    NationalIdentityNumber = "12345678901",
                    AddressInfo =
                    [
                        new SmsAddressPoint("+4799999999"),
                        new EmailAddressPoint("recipient@example.com")
                    ]
                }
            ]
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        emailProcessingServiceMock.Verify(
            s => s.ProcessOrderWithoutAddressLookup(order, It.Is<List<Recipient>>(r => r.Count == 1 && r[0].AddressInfo.Count == 1 && r[0].AddressInfo[0] is EmailAddressPoint)),
            Times.Once);

        smsProcessingServiceMock.Verify(
            s => s.ProcessOrderWithoutAddressLookup(order, It.Is<List<Recipient>>(r => r.Count == 1 && r[0].AddressInfo.Count == 1 && r[0].AddressInfo[0] is SmsAddressPoint)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_WithSmsAndEmailRecipients_CallsRespectiveRetryServices()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients =
            [
                new Recipient
                {
                    NationalIdentityNumber = "24288222432",
                    AddressInfo =
                    [
                        new SmsAddressPoint("+4799999999"),
                        new EmailAddressPoint("recipient@example.com")
                    ]
                }
            ]
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        emailProcessingServiceMock.Verify(
            s => s.ProcessOrderRetryWithoutAddressLookup(order, It.Is<List<Recipient>>(r => r.Count == 1 && r[0].AddressInfo.Count == 1 && r[0].AddressInfo[0] is EmailAddressPoint)),
            Times.Once);

        smsProcessingServiceMock.Verify(
            s => s.ProcessOrderRetryWithoutAddressLookup(order, It.Is<List<Recipient>>(r => r.Count == 1 && r[0].AddressInfo.Count == 1 && r[0].AddressInfo[0] is SmsAddressPoint)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_WithMixedContactPoints_SeparatesChannelsCorrectly()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients = [
                new Recipient
                {
                    NationalIdentityNumber = "10277990119",
                    AddressInfo =
                    [
                        new SmsAddressPoint("+4799999999"),
                        new EmailAddressPoint("person@example.com")
                    ]
                }
            ]
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        List<Recipient>? capturedSmsRecipients = null;
        List<Recipient>? capturedEmailRecipients = null;

        emailProcessingServiceMock
            .Setup(s => s.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
            .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedEmailRecipients = recipients)
            .Returns(Task.CompletedTask);

        smsProcessingServiceMock
            .Setup(s => s.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
            .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedSmsRecipients = recipients)
            .Returns(Task.CompletedTask);

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        Assert.NotNull(capturedSmsRecipients);
        Assert.NotNull(capturedEmailRecipients);

        Assert.Single(capturedSmsRecipients);
        Assert.Single(capturedEmailRecipients);

        Assert.Equal("10277990119", capturedSmsRecipients[0].NationalIdentityNumber);
        Assert.Equal("10277990119", capturedEmailRecipients[0].NationalIdentityNumber);

        Assert.Single(capturedSmsRecipients[0].AddressInfo);
        Assert.Single(capturedEmailRecipients[0].AddressInfo);

        Assert.IsType<SmsAddressPoint>(capturedSmsRecipients[0].AddressInfo[0]);
        Assert.IsType<EmailAddressPoint>(capturedEmailRecipients[0].AddressInfo[0]);

        Assert.Equal("+4799999999", ((SmsAddressPoint)capturedSmsRecipients[0].AddressInfo[0]).MobileNumber);
        Assert.Equal("person@example.com", ((EmailAddressPoint)capturedEmailRecipients[0].AddressInfo[0]).EmailAddress);
    }

    [Fact]
    public async Task ProcessOrderRetry_WithOnlyEmailContacts_OnlyCallsEmailProcessingService()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients =
            [
                new Recipient
                {
                    OrganizationNumber = "01885298520",
                    AddressInfo =
                    [
                        new EmailAddressPoint("org@example.com")
                    ]
                }
            ]
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        // Capture args for verification
        List<Recipient>? capturedEmailRecipients = null;

        emailProcessingServiceMock
            .Setup(s => s.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
            .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedEmailRecipients = recipients)
            .Returns(Task.CompletedTask);

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        Assert.NotNull(capturedEmailRecipients);
        Assert.Single(capturedEmailRecipients);

        Assert.Equal("01885298520", capturedEmailRecipients[0].OrganizationNumber);

        Assert.Single(capturedEmailRecipients[0].AddressInfo);
        Assert.IsType<EmailAddressPoint>(capturedEmailRecipients[0].AddressInfo[0]);

        smsProcessingServiceMock.Verify(
            s => s.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(r => r.Count == 0)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_WithOnlySmsContacts_OnlyCallsSmsProcessingService()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients =
            [
                new Recipient
                {
                    NationalIdentityNumber = "18826599975",
                    AddressInfo =
                    [
                        new SmsAddressPoint("+4712345678")
                    ]
                }
            ]
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        // Capture args for verification
        List<Recipient>? capturedSmsRecipients = null;

        smsProcessingServiceMock
            .Setup(s => s.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
            .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedSmsRecipients = recipients)
            .Returns(Task.CompletedTask);

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrder(order);

        // Assert
        Assert.NotNull(capturedSmsRecipients);
        Assert.Single(capturedSmsRecipients);

        Assert.Equal("18826599975", capturedSmsRecipients[0].NationalIdentityNumber);

        Assert.Single(capturedSmsRecipients[0].AddressInfo);
        Assert.IsType<SmsAddressPoint>(capturedSmsRecipients[0].AddressInfo[0]);

        // Verify Email service was not called with any recipients
        emailProcessingServiceMock.Verify(
            s => s.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(r => r.Count == 0)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderRetry_WithNoContacts_DoesNotCallProcessingServices()
    {
        // Arrange
        var order = new NotificationOrder
        {
            Recipients =
            [
                new Recipient
                    {
                        AddressInfo = [],
                        NationalIdentityNumber = "04917199103"
                    }
            ]
        };

        var contactPointServiceMock = new Mock<IContactPointService>();
        contactPointServiceMock.Setup(s => s.AddEmailAndSmsContactPointsAsync(It.IsAny<List<Recipient>>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var smsProcessingServiceMock = new Mock<ISmsOrderProcessingService>();
        var emailProcessingServiceMock = new Mock<IEmailOrderProcessingService>();

        var service = new EmailAndSmsOrderProcessingService(emailProcessingServiceMock.Object, smsProcessingServiceMock.Object, contactPointServiceMock.Object);

        // Act
        await service.ProcessOrderRetry(order);

        // Assert
        // Both services should be called with empty recipient lists
        emailProcessingServiceMock.Verify(
            s => s.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(r => r.Count == 0)),
            Times.Once);

        smsProcessingServiceMock.Verify(
            s => s.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(r => r.Count == 0)),
            Times.Once);
    }
}
