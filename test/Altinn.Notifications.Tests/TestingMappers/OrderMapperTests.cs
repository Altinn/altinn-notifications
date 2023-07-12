﻿using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.TestingMappers;
public class OrderMapperTests
{

    [Fact]
    public void MapToNotificationOrderExt_AreEquivalent()
    {
        DateTime sendTime = DateTime.UtcNow;
        // Arrange 
        NotificationOrder order = new("ref1337", new List<INotificationTemplate>(), sendTime, NotificationChannel.Email, new Creator("ttd"), new List<Recipient>());
        NotificationOrderExt expected = new()
        {
            Id = order.Id,
            Creator = "ttd",
            NotificationChannel = NotificationChannel.Email,
            SendersReference = "ref1337",
            SendTime = sendTime,
            Recipients = new List<RecipientExt>(),
            EmailTemplate = null
        };

        // Act
        var actual = OrderMapper.MapToNotificationOrderExt(order);

        // Assert
        Assert.Equivalent(expected, actual, true);
    }

    [Fact]
    public void MapToRecipientExt_AreEquivalent()
    {

        // Arrange 
        Recipient input = new()
        {
            RecipientId = "16069412345",
            AddressInfo = new List<IAddressPoint>()
            {
               new EmailAddressPoint("input@domain.com")
            }
        };

        RecipientExt expected = new()
        {
            Id = "16069412345",
            EmailAddress = "input@domain.com"
        };

        // Act
        var actual = OrderMapper.MapToRecipientExt(new List<Recipient>() { input });

        // Assert
        Assert.Equivalent(new List<RecipientExt>() { expected }, actual, true);
    }

    [Fact]
    public void MapToOrderRequest_ToAddressesProvided_AreEquivalent()
    {
        DateTime sendTime = DateTime.UtcNow;

        // Arrange 
        EmailNotificationOrderRequestExt orderRequestExt = new()
        {
            Body = "email-body",
            ContentType = EmailContentType.Html,
            FromAddress = "sender@domain.com",
            Recipients = null,
            SendersReference = "senders-reference",
            SendTime = sendTime,
            Subject = "email-subject",
            ToAddresses = new List<string>() { "recipient1@domain.com", "recipient2@domain.com" }
        };

        NotificationOrderRequest expected = new(
            "senders-reference",
            new List<INotificationTemplate>() {
                new EmailTemplate(
                    "sender@domain.com",
                    "email-subject",
                    "email-body",
                    EmailContentType.Html)},
            sendTime,
            NotificationChannel.Email,
            new List<Recipient>() {
                new Recipient(new List<IAddressPoint>(){new EmailAddressPoint("recipient1@domain.com")}),
                new Recipient(new List<IAddressPoint>(){ new EmailAddressPoint("recipient2@domain.com")})
            }
        );

        // Act
        var actual = OrderMapper.MapToOrderRequest(orderRequestExt);

        // Assert
        Assert.Equivalent(expected, actual, true);
    }
}
