﻿using System.Collections.Generic;
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
}
