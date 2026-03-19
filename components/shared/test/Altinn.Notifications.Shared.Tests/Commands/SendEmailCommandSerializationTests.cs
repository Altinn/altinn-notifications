using System.Text.Json;

using Altinn.Notifications.Shared.Commands;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Commands;

public class SendEmailCommandSerializationTests
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = false };

    [Fact]
    public void SendEmailCommand_Serializes_WithExpectedJsonPropertyNames()
    {
        var sendEmailCommand = new SendEmailCommand
        {
            Body = "body",
            Subject = "subject",
            ContentType = "Plain",
            NotificationId = Guid.Empty,
            ToAddress = "to@example.com",
            FromAddress = "from@example.com"
        };

        var json = JsonSerializer.Serialize(sendEmailCommand, _options);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("body", out _), "Expected property 'body' not found.");
        Assert.True(root.TryGetProperty("subject", out _), "Expected property 'subject' not found.");
        Assert.True(root.TryGetProperty("toAddress", out _), "Expected property 'toAddress' not found.");
        Assert.True(root.TryGetProperty("fromAddress", out _), "Expected property 'fromAddress' not found.");
        Assert.True(root.TryGetProperty("contentType", out _), "Expected property 'contentType' not found.");
        Assert.True(root.TryGetProperty("notificationId", out _), "Expected property 'notificationId' not found.");
    }

    [Fact]
    public void SendEmailCommand_Deserializes_FromExpectedJsonPropertyNames()
    {
        const string sendEmailCommandJson = """
            {
                "body": "body",
                "subject": "subject",
                "contentType": "Html",
                "toAddress": "to@example.com",
                "fromAddress": "from@example.com",
                "notificationId": "00000000-0000-0000-0000-000000000001"
            }
            """;

        var command = JsonSerializer.Deserialize<SendEmailCommand>(sendEmailCommandJson, _options);

        Assert.NotNull(command);
        Assert.Equal("body", command.Body);
        Assert.Equal("subject", command.Subject);
        Assert.Equal("Html", command.ContentType);
        Assert.Equal("to@example.com", command.ToAddress);
        Assert.Equal("from@example.com", command.FromAddress);
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), command.NotificationId);
    }
}
