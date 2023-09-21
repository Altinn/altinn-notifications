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
            RequestedSendTime = _requestedSendTime,
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
            { "requestedSendTime", "2023-02-14T08:15:00Z" },
            { "notificationChannel", "Email" },
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
                            "recipientId", "recipient1"
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
                    },
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

    [Theory]
    [InlineData(1, "{ \"id\": \"4fec2be9-7f52-4d32-9554-467908c3c629\", \"created\": \"2023-07-14T07:39:19.088978Z\", \"creator\": { \"shortName\": \"ttd\" }, \"requestedSendTime\": \"2023-08-14T08:15:00Z\", \"templates\": [ { \"$\": \"email\", \"body\": \"email-body\", \"type\": \"Email\", \"subject\": \"email-subject\", \"contentType\": \"Html\", \"fromAddress\": \"sender@domain.com\" } ], \"recipients\": [ { \"addressInfo\": [ { \"$\": \"email\", \"addressType\": \"Email\", \"emailAddress\": \"recipient1@domain.com\" } ], \"recipientId\": \"\" }, { \"addressInfo\": [ { \"$\": \"email\", \"addressType\": \"Email\", \"emailAddress\": \"recipient2@domain.com\" } ], \"recipientId\": \"\" } ], \"sendersReference\": \"senders-reference\", \"notificationChannel\": \"Email\" }")]
#pragma warning disable xUnit1026, IDE0060// Theory methods should use all of their parameters and Remove unused parameter
    public void Deserialize(int exampleNo, string serializedOrder)
#pragma warning restore xUnit1026, IDE0060
    {
        var actual = NotificationOrder.Deserialize(serializedOrder);
        Assert.NotNull(actual);
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
