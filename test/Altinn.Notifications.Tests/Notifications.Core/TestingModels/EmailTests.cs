using System;
using System.Text.Json.Nodes;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels;
public class EmailTests
{
    private readonly Email _email;
    private readonly string _serializedEmail;

    public EmailTests()
    {
        Guid id = Guid.NewGuid();
        _email = new Email(id, "subject", "body", "from@domain.com", "to@domain.com", EmailContentType.Html);
        _serializedEmail = new JsonObject()
        {
            { "notificationId", id },
             { "subject", "subject" },
            {"body", "body" },
            {"fromAddress", "from@domain.com" },
            {"toAddress", "to@domain.com" },
            {"contentType", "Html" },

        }.ToJsonString();
    }


    [Fact]
    public void SerializeToJson()
    {
        // Arrange
        string expected = _serializedEmail;

        // Act
        var actual = _email.Serialize();

        // Assert
        Assert.Equal(expected, actual);
    }
}
