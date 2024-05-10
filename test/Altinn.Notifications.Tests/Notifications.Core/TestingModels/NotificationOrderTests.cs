using System;
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

        _order = NotificationOrder
            .GetBuilder()
            .SetId(id)
            .SetSendersReference("senders-reference")
            .SetTemplates(
            [
                new EmailTemplate()
                {
                    Type = NotificationTemplateType.Email,
                    FromAddress = "sender@domain.com",
                    Subject = "email-subject",
                    Body = "email-body",
                    ContentType = EmailContentType.Html
                }
            ])
            .SetRequestedSendTime(_requestedSendTime)
            .SetNotificationChannel(NotificationChannel.Email)
            .SetIgnoreReservation(false)
            .SetCreator(new Creator("ttd"))
            .SetCreated(_createdTime)
            .SetRecipients([
                new Recipient()
                {
                    NationalIdentityNumber = "nationalidentitynumber",
                    IsReserved = false,
                    AddressInfo =
                    [
                        new EmailAddressPoint()
                        {
                            AddressType = AddressType.Email,
                            EmailAddress = "recipient1@domain.com"
                        }
                    ]
                }
            ])
            .Build();

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
            {
                "templates",  new JsonArray()
                {
                    new JsonObject()
                    {
                        { "$", "email" },
                        { "type", "Email" },
                        { "fromAddress", "sender@domain.com" },
                        { "subject", "email-subject" },
                        { "body", "email-body" },
                        { "contentType", "Html" }
                    }
                }
            },
            {
                "recipients", new JsonArray()
                {
                    new JsonObject()
                    {
                        {
                            "nationalIdentityNumber", "nationalidentitynumber"
                        },
                        {
                            "isReserved", false
                        },
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
