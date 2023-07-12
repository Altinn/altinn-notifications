using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.TestingModels;
public class NotificationOrderRequestExtTests
{
    [Fact]
    public void SerializeToJson()
    {
        DateTime sendTime = new(2023, 02, 14, 08, 15, 00, DateTimeKind.Utc);
        DateTime createdTime = new(2022, 02, 14, 08, 15, 00, DateTimeKind.Utc);
        string id = Guid.NewGuid().ToString();

        JsonObject serializedOrderRequest = new()
        {
            { "id", id },
            { "creator", "ttd" },
            { "sendersReference", "senders-reference" },
            { "sendTime", "2023-02-14T08:15:00Z"},
            { "created", "2022-02-14T08:15:00Z"},
            { "notificationChannel", "Email" },
            { "recipients", new JsonArray()
                {
                    new JsonObject() {{ "emailAddress","recipient1@domain.com" }},
                    new JsonObject() {{ "emailAddress","recipient2@domain.com" }}
                }
            },
            {"emailTemplate",  new JsonObject()
                {
                    {"fromAddress","sender@domain.com" },
                    {"subject", "email-subject" },
                    {"body","email-body" },
                    {"content-type", "Html" }
                }
            }
        };

        string expected = serializedOrderRequest.ToJsonString();

        NotificationOrderExt orderRequest = new()
        {
            Id = id,
            Creator = "ttd",
            Created = createdTime,
            SendersReference = "senders-reference",
            SendTime = sendTime,
            NotificationChannel = NotificationChannel.Email,
            Recipients = new List<RecipientExt>() {
                  new RecipientExt{ EmailAddress="recipient1@domain.com" },
                  new RecipientExt{ EmailAddress="recipient2@domain.com" }
            },
            EmailTemplate = new EmailTemplateExt
            {
                FromAddress = "sender@domain.com",
                Subject = "email-subject",
                Body = "email-body",
                ContentType = EmailContentType.Html,
            }
        };

        // Act
        var actual = orderRequest.Serialize();

        // Assert
        Assert.Equal(expected, actual);


    }
}