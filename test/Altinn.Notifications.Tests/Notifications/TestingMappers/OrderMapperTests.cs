using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class OrderMapperTests
{
    public OrderMapperTests()
    {
        ResourceLinkExtensions.Initialize("https://platform.at22.altinn.cloud");
    }

    [Fact]
    public void MapToNotificationOrderExt_AreEquivalent()
    {
        // Arrange 
        DateTime sendTime = DateTime.UtcNow;
        DateTime created = DateTime.UtcNow;

        NotificationOrder order = new()
        {
            Id = Guid.NewGuid(),
            SendersReference = "ref1337",
            Templates = [
                new EmailTemplate("from@domain.com", "email-subject", "email-body", EmailContentType.Plain),
                new SmsTemplate("Altinn-test", "This is a text message")
            ],
            RequestedSendTime = sendTime,
            NotificationChannel = NotificationChannel.Email,
            Creator = new Creator("ttd"),
            Created = created,
            Recipients = [],
            IgnoreReservation = true
        };

        NotificationOrderExt expected = new()
        {
            Id = order.Id.ToString(),
            Creator = "ttd",
            Created = created,
            NotificationChannel = NotificationChannelExt.Email,
            SendersReference = "ref1337",
            RequestedSendTime = sendTime,
            Recipients = new List<RecipientExt>(),
            EmailTemplate = new EmailTemplateExt
            {
                Body = "email-body",
                ContentType = EmailContentTypeExt.Plain,
                FromAddress = "from@domain.com",
                Subject = "email-subject"
            },
            SmsTemplate = new SmsTemplateExt
            {
                Body = "This is a text message",
                SenderNumber = "Altinn-test"
            },
            Links = new OrderResourceLinksExt()
            {
                Self = $"https://platform.at22.altinn.cloud/notifications/api/v1/orders/{order.Id}",
                Status = $"https://platform.at22.altinn.cloud/notifications/api/v1/orders/{order.Id}/status"
            },
            IgnoreReservation = true
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
            NationalIdentityNumber = "16069412345",
            AddressInfo = new List<IAddressPoint>()
            {
                new EmailAddressPoint("input@domain.com"),
                new SmsAddressPoint("+4740000001")
            }
        };

        RecipientExt expected = new()
        {
            NationalIdentityNumber = "16069412345",
            EmailAddress = "input@domain.com",
            MobileNumber = "+4740000001"
        };

        // Act
        var actual = new List<Recipient>() { input }.MapToRecipientExt();

        // Assert
        Assert.Equivalent(new List<RecipientExt>() { expected }, actual, true);
    }

    [Fact]
    public void ForEmailMapToOrderRequest_RecipientsProvided_AreEquivalent()
    {
        DateTime sendTime = DateTime.UtcNow;

        // Arrange 
        EmailNotificationOrderRequestExt orderRequestExt = new()
        {
            Body = "email-body",
            ContentType = EmailContentTypeExt.Html,
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient1@domain.com" }, new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            Subject = "email-subject",
        };

        NotificationOrderRequest expected = new()
        {
            SendersReference = "senders-reference",
            Creator = new Creator("ttd"),
            Templates = new List<INotificationTemplate>()
            {
                new EmailTemplate(
                    string.Empty,
                    "email-subject",
                    "email-body",
                    EmailContentType.Html)
            },
            RequestedSendTime = sendTime,
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<Recipient>()
            {
                        new Recipient() { AddressInfo = new List<IAddressPoint>() { new EmailAddressPoint("recipient1@domain.com") } },
                        new Recipient() { AddressInfo = new List<IAddressPoint>() { new EmailAddressPoint("recipient2@domain.com") } }
            }
        };

        // Act
        var actual = orderRequestExt.MapToOrderRequest("ttd");

        // Assert
        Assert.Equivalent(expected, actual, true);
    }

    [Fact]
    public void ForEmailMapToOrderRequest_SendTimeLocalConvertedToUtc_AreEquivalent()
    {
        DateTime sendTime = DateTime.Now; // Setting the time in Local time zone

        // Arrange
        EmailNotificationOrderRequestExt orderRequestExt = new()
        {
            Body = "email-body",
            ContentType = EmailContentTypeExt.Html,
            RequestedSendTime = sendTime, // Local time zone
            Subject = "email-subject",
            IgnoreReservation = true
        };

        NotificationOrderRequest expected = new()
        {
            Creator = new Creator("ttd"),
            Templates = new List<INotificationTemplate>()
            {
                new EmailTemplate(
                    string.Empty,
                    "email-subject",
                    "email-body",
                    EmailContentType.Html)
            },
            RequestedSendTime = sendTime.ToUniversalTime(),  // Expecting the time in UTC time zone
            NotificationChannel = NotificationChannel.Email,
            IgnoreReservation = true
        };

        // Act
        var actual = orderRequestExt.MapToOrderRequest("ttd");

        // Assert
        Assert.Equivalent(expected, actual, true);
    }

    [Fact]
    public void ForSmsMapToOrderRequest_RecipientsProvided_AreEquivalent()
    {
        DateTime sendTime = DateTime.UtcNow;

        // Arrange 
        SmsNotificationOrderRequestExt orderRequestExt = new()
        {
            Body = "sms-body",
            Recipients = new List<RecipientExt>()
            {
                new RecipientExt() { MobileNumber = "+4740000001" },
                new RecipientExt() { MobileNumber = "+4740000002" }
            },
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime
        };

        NotificationOrderRequest expected = new()
        {
            SendersReference = "senders-reference",
            Creator = new Creator("ttd"),
            Templates = new List<INotificationTemplate>()
            {
                new SmsTemplate(
                    string.Empty,
                    "sms-body")
            },
            RequestedSendTime = sendTime,
            NotificationChannel = NotificationChannel.Sms,
            Recipients = new List<Recipient>()
            {
                        new Recipient() { AddressInfo = new List<IAddressPoint>() { new SmsAddressPoint("+4740000001") } },
                        new Recipient() { AddressInfo = new List<IAddressPoint>() { new SmsAddressPoint("+4740000002") } }
            }
        };

        // Act
        var actual = orderRequestExt.MapToOrderRequest("ttd");

        // Assert
        Assert.Equivalent(expected, actual, true);
    }

    [Fact]
    public void ForSmsMapToOrderRequest_SendTimeLocalConvertedToUtc_AreEquivalent()
    {
        DateTime sendTime = DateTime.Now; // Setting the time in Local time zone

        // Arrange
        SmsNotificationOrderRequestExt orderRequestExt = new()
        {
            Body = "sms-body",
            RequestedSendTime = sendTime // Local time zone
        };

        NotificationOrderRequest expected = new()
        {
            Creator = new Creator("ttd"),
            Templates = new List<INotificationTemplate>()
            {
                new SmsTemplate(
                    string.Empty,
                    "sms-body")
            },
            RequestedSendTime = sendTime.ToUniversalTime(),  // Expecting the time in UTC time zone
            NotificationChannel = NotificationChannel.Sms
        };

        // Act
        var actual = orderRequestExt.MapToOrderRequest("ttd");

        // Assert
        Assert.Equivalent(expected, actual, true);
    }

    [Theory]
    [InlineData(NotificationChannelExt.Email, NotificationChannel.Email)]
    [InlineData(NotificationChannelExt.EmailPreferred, NotificationChannel.EmailPreferred)]
    [InlineData(NotificationChannelExt.Sms, NotificationChannel.Sms)]
    [InlineData(NotificationChannelExt.SmsPreferred, NotificationChannel.SmsPreferred)]
    public void MapToOrderRequest_AreEquivalent(NotificationChannelExt extChannel, NotificationChannel expectedChannel)
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;

        NotificationOrderRequestExt ext = new()
        {
            NotificationChannel = extChannel,
            EmailTemplate = new()
            {
                Subject = "email-subject",
                Body = "email-body",
                ContentType = EmailContentTypeExt.Html
            },
            SmsTemplate = new()
            {
                Body = "sms-body",
            },
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient1@domain.com" }, new RecipientExt() { NationalIdentityNumber = "123456" } },
            SendersReference = "senders-reference",
            RequestedSendTime = sendTime,
            ConditionEndpoint = new Uri("https://vg.no"),
            IgnoreReservation = true,
            ResourceId = "urn:altinn:resource:test"
        };

        NotificationOrderRequest expected = new()
        {
            SendersReference = "senders-reference",
            Creator = new Creator("ttd"),
            Templates = new List<INotificationTemplate>()
            {
                new EmailTemplate(
                    string.Empty,
                    "email-subject",
                    "email-body",
                    EmailContentType.Html),
                new SmsTemplate(
                    string.Empty,
                    "sms-body")
            },
            RequestedSendTime = sendTime,
            Recipients = new List<Recipient>()
            {
                        new Recipient() { AddressInfo = new List<IAddressPoint>() { new EmailAddressPoint("recipient1@domain.com") } },
                        new Recipient() {NationalIdentityNumber = "123456" }
            },
            ConditionEndpoint = new Uri("https://vg.no"),
            IgnoreReservation = true,
            ResourceId = "urn:altinn:resource:test"
        };

        expected.NotificationChannel = expectedChannel;

        // Act
        var actual = ext.MapToOrderRequest("ttd");

        // Assert
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public void MapToNotificationOrderWithStatusExt_EmailStatusProvided_AreEquivalent()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime created = DateTime.UtcNow;
        DateTime lastUpdated = DateTime.UtcNow;

        NotificationOrderWithStatus orderToMap = new()
        {
            Id = Guid.NewGuid(),
            Created = created,
            Creator = new("ttd"),
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = sendTime,
            SendersReference = "senders-ref",
            ProcessingStatus = new()
            {
                LastUpdate = lastUpdated,
                Status = OrderProcessingStatus.Registered,
                StatusDescription = "The order has been registered, but not processed yet. No notifications are generated"
            },
            NotificationStatuses = new()
            {
                { NotificationTemplateType.Email, new NotificationStatus() { Generated = 15, Succeeded = 10 } }
            }
        };

        NotificationOrderWithStatusExt expected = new()
        {
            Id = orderToMap.Id.ToString(),
            Created = created,
            Creator = "ttd",
            NotificationChannel = NotificationChannelExt.Email,
            RequestedSendTime = sendTime,
            SendersReference = "senders-ref",
            ProcessingStatus = new()
            {
                Status = "Registered",
                StatusDescription = "The order has been registered, but not processed yet. No notifications are generated",
                LastUpdate = lastUpdated
            },
            NotificationsStatusSummary = new NotificationsStatusSummaryExt()
            {
                Email = new()
                {
                    Generated = 15,
                    Succeeded = 10,
                    Links = new()
                    {
                        Self = $"https://platform.at22.altinn.cloud/notifications/api/v1/orders/{orderToMap.Id}/notifications/email"
                    }
                }
            }
        };

        // Act
        var actual = orderToMap.MapToNotificationOrderWithStatusExt();

        // Assert
        Assert.Equivalent(expected, actual, true);
    }

    [Fact]
    public void MapToNotificationOrderWithStatusExt_NoNotificationStatusProvided_AreEquivalent()
    {
        // Arrange
        DateTime sendTime = DateTime.UtcNow;
        DateTime created = DateTime.UtcNow;
        DateTime lastUpdated = DateTime.UtcNow;

        NotificationOrderWithStatus orderToMap = new()
        {
            Id = Guid.NewGuid(),
            Created = created,
            Creator = new("ttd"),
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = sendTime,
            SendersReference = "senders-ref",
            ProcessingStatus = new()
            {
                LastUpdate = lastUpdated,
                Status = OrderProcessingStatus.Registered,
                StatusDescription = "The order has been registered, but not processed yet. No notifications are generated"
            }
        };

        NotificationOrderWithStatusExt expected = new()
        {
            Id = orderToMap.Id.ToString(),
            Created = created,
            Creator = "ttd",
            NotificationChannel = NotificationChannelExt.Email,
            RequestedSendTime = sendTime,
            SendersReference = "senders-ref",
            ProcessingStatus = new()
            {
                Status = "Registered",
                StatusDescription = "The order has been registered, but not processed yet. No notifications are generated",
                LastUpdate = lastUpdated
            }
        };

        // Act
        var actual = orderToMap.MapToNotificationOrderWithStatusExt();

        // Assert
        Assert.Equivalent(expected, actual, true);
    }
}
