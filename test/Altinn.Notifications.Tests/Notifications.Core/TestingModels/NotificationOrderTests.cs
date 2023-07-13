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
    private readonly DateTime _sendTime = new(2023, 02, 14, 08, 15, 00, DateTimeKind.Utc);
    private readonly DateTime _createdTime = new(2022, 02, 14, 08, 15, 00, DateTimeKind.Utc);

    public NotificationOrderTests()
    {
        string id = Guid.NewGuid().ToString();

        _order = new()
        {
            Id = id,
            SendersReference = "senders-reference",
            Templates = new List<INotificationTemplate>()
             {
                 new EmailTemplate()
                 {
                     Type = NotificationTemplateType.Email,
                     FromAddress = "sender@domain.com",
                     Subject = "email-subject",
                     Body = "email-body",
                     ContentType = EmailContentType.Html
                 }
            },
            SendTime = _sendTime,
            NotificationChannel = NotificationChannel.Email,
            Creator = new("ttd"),
            Created = _createdTime,
            Recipients = new List<Recipient>()
            {
                new Recipient()
                {
                    RecipientId = "recipient1",
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
            {
                "templates",  new JsonArray()
                {
                    new JsonObject() {
                        {"$descriminator", "email" },
                        { "type","Email" },
                        {"fromAddress","sender@domain.com" },
                        {"subject", "email-subject" },
                        {"body","email-body" },
                        {"contentType", "Html" }
                    }
                }
            },
            { "sendTime", "2023-02-14T08:15:00Z"},
            { "notificationChannel", "Email" },
            { "creator", new JsonObject() {
                { "shortName", "ttd" }
            }},
            { "created", "2022-02-14T08:15:00Z"},
            { "recipients", new JsonArray()
                {
                    new JsonObject() {
                        { "recipientId", "recipient1" },
                        { "addressInfo",  new JsonArray()
                            {
                             new JsonObject()
                             {
                                 {"$descriminator", "email" },
                                 {"addressType", "Email" },
                                 { "emailAddress", "recipient1@domain.com" }
                             }
                            }
                        }
                    },
                }
            }
        }.ToJsonString();
    }

    [Fact]
    public void Deserialize()
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
}
