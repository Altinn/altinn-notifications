using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels;

public class NotificationOrderTests
{
    private readonly string _serializedOrder;
    private readonly NotificationOrder _order;
    private readonly DateTime _requestedSendTime = new(2023, 02, 14, 08, 15, 00, DateTimeKind.Utc);
    private readonly DateTime _createdTime = new(2022, 02, 14, 08, 15, 00, DateTimeKind.Utc);

    public NotificationOrderTests()
    {
        Guid id = Guid.NewGuid();

        _order = new()
        {
            Id = id,
            Type = OrderTypes.Notification,
            SendersReference = "senders-reference",
            Templates = new List<INotificationTemplate>()
            {
                 new EmailTemplate()
                 {
                     FromAddress = "sender@domain.com",
                     Subject = "email-subject",
                     Body = "email-body",
                     ContentType = EmailContentType.Html
                 }
            },
            RequestedSendTime = _requestedSendTime,
            NotificationChannel = NotificationChannel.Email,
            IgnoreReservation = false,
            Creator = new("ttd"),
            Created = _createdTime,
            Recipients = new List<Recipient>()
            {
                new Recipient()
                {
                    NationalIdentityNumber = "nationalidentitynumber",
                    IsReserved = false,
                    AddressInfo = new()
                    {
                        new EmailAddressPoint()
                        {
                            AddressType = AddressType.Email,
                            EmailAddress = "recipient1@domain.com"
                        }
                    }
                }
            }
        };

        _serializedOrder = new JsonObject()
        {
            { "id", id },
            { "sendersReference", "senders-reference" },
            { "requestedSendTime", "2023-02-14T08:15:00Z" },
            { "notificationChannel", "Email" },
            { "ignoreReservation", false },
            {
                "creator", new JsonObject()
                {
                    { "shortName", "ttd" }
                }
            },
            { "created", "2022-02-14T08:15:00Z" },
            { "type", "Notification" },
            {
                "templates",  new JsonArray()
                {
                    new JsonObject()
                    {
                        { "$", "email" },
                        { "body", "email-body" },
                        { "contentType", "Html" },
                        { "fromAddress", "sender@domain.com" },
                        { "subject", "email-subject" },
                        { "type", "Email" }
                    }
                }
            },
            {
                "recipients", new JsonArray()
                {
                    new JsonObject()
                    {
                        {
                            "addressInfo",  new JsonArray()
                            {
                             new JsonObject()
                             {
                                 { "$", "email" },
                                 { "addressType", "Email" },
                                 { "emailAddress", "recipient1@domain.com" }
                             }
                            }
                        },
                        {
                            "isReserved", false
                        },
                        {
                            "nationalIdentityNumber", "nationalidentitynumber"
                        }
                    }
                }
            }
        }.ToJsonString();
    }

    [Fact]
    public void Deserialize_Static()
    {
        // Arrange
        NotificationOrder expected = _order;

        // Act
        var actual = NotificationOrder.Deserialize(_serializedOrder);

        // Assert
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public void SerializeToJson()
    {
        // Arrange
        string expected = _serializedOrder;

        // Act
        var actual = _order.Serialize();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryParse_EmptyString_False()
    {
        bool actualResult = NotificationOrder.TryParse(string.Empty, out _);
        Assert.False(actualResult);
    }

    [Fact]
    public void TryParse_InvalidString_False()
    {
        bool actualResult = NotificationOrder.TryParse("{\"ticket\":\"noTicket\"}", out _);

        Assert.False(actualResult);
    }

    [Fact]
    public void TryParse_InvalidJsonExceptionThrown_False()
    {
        bool actualResult = NotificationOrder.TryParse("{\"ticket:\"noTicket\"}", out _);

        Assert.False(actualResult);
    }
}
