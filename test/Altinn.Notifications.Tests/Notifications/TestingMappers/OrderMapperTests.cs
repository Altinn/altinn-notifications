using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;
public class OrderMapperTests
{
    [Fact]
    public void MapToNotificationOrderExt_AreEquivalent()
    {
        // Arrange 
        DateTime sendTime = DateTime.UtcNow;
        DateTime created = DateTime.UtcNow;

        NotificationOrder order = new()
        {
            Id = Guid.NewGuid().ToString(),
            SendersReference = "ref1337",
            Templates = new List<INotificationTemplate>() { new EmailTemplate("from@domain.com", "email-subject", "email-body", EmailContentType.Plain) },
            SendTime = sendTime,
            NotificationChannel = NotificationChannel.Email,
            Creator = new Creator("ttd"),
            Created = created,
            Recipients = new List<Recipient>()
        };

        NotificationOrderExt expected = new()
        {
            Id = order.Id,
            Creator = "ttd",
            Created = created,
            NotificationChannel = NotificationChannel.Email,
            SendersReference = "ref1337",
            SendTime = sendTime,
            Recipients = new List<RecipientExt>(),
            EmailTemplate = new EmailTemplateExt
            {
                Body = "email-body",
                ContentType = EmailContentType.Plain,
                FromAddress = "from@domain.com",
                Subject = "email-subject"
            }
        };

        // Act
        var actual = order.MapToNotificationOrderExt();

        // Assert
        Assert.Equivalent(expected, actual, true);
    }

    [Fact]
    public void MapToRecipientExt_AllPropertiesPresent_AreEquivalent()
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
        var actual = new List<Recipient>() { input }.MapToRecipientExt();

        // Assert
        Assert.Equivalent(new List<RecipientExt>() { expected }, actual, true);
    }

    [Fact]
    public void MapToRecipientExt_NoEmailPresent_AreEquivalent()
    {

        // Arrange 
        Recipient input = new()
        {
            RecipientId = "16069412345"
        };

        RecipientExt expected = new()
        {
            Id = "16069412345"
        };

        // Act
        var actual = new List<Recipient>() { input }.MapToRecipientExt();

        // Assert
        Assert.Equivalent(new List<RecipientExt>() { expected }, actual, true);
    }

    [Fact]
    public void MapToOrderRequest_RecipientsProvided_AreEquivalent()
    {
        DateTime sendTime = DateTime.UtcNow;

        // Arrange 
        EmailNotificationOrderRequestExt orderRequestExt = new()
        {
            Body = "email-body",
            ContentType = EmailContentType.Html,
            FromAddress = "sender@domain.com",
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient1@domain.com" }, new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
            SendersReference = "senders-reference",
            SendTime = sendTime,
            Subject = "email-subject",
        };

        NotificationOrderRequest expected = new()
        {
            SendersReference = "senders-reference",
            Creator = new Creator("ttd"),
            Templates = new List<INotificationTemplate>() {
                new EmailTemplate(
                    "sender@domain.com",
                    "email-subject",
                    "email-body",
                    EmailContentType.Html)},
            SendTime = sendTime,
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>() {
                        new Recipient(){ AddressInfo =new List<IAddressPoint>(){new EmailAddressPoint("recipient1@domain.com")}},
                        new Recipient(){ AddressInfo =new List<IAddressPoint>(){new EmailAddressPoint("recipient2@domain.com")}}
            }
        };

        // Act
        var actual = orderRequestExt.MapToOrderRequest("ttd");

        // Assert
        Assert.Equivalent(expected, actual, true);
    }
}
