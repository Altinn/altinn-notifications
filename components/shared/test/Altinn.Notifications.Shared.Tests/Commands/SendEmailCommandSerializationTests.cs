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
            FromAddress = "sender@altinnxyz.no",
            ToAddress = "recipient@altinnxyz.no"
        };

        var serializedString = JsonSerializer.Serialize(sendEmailCommand, _options);
        var jsonDocument = JsonDocument.Parse(serializedString);
        var jsonElement = jsonDocument.RootElement;

        Assert.True(jsonElement.TryGetProperty("body", out _), "Expected property 'body' not found.");
        Assert.True(jsonElement.TryGetProperty("subject", out _), "Expected property 'subject' not found.");
        Assert.True(jsonElement.TryGetProperty("toAddress", out _), "Expected property 'toAddress' not found.");
        Assert.True(jsonElement.TryGetProperty("fromAddress", out _), "Expected property 'fromAddress' not found.");
        Assert.True(jsonElement.TryGetProperty("contentType", out _), "Expected property 'contentType' not found.");
        Assert.True(jsonElement.TryGetProperty("notificationId", out _), "Expected property 'notificationId' not found.");
    }

    [Fact]
    public void SendEmailCommand_Deserializes_FromExpectedJsonPropertyNames()
    {
        const string sendEmailCommandJson = """
            {
                "body": "body",
                "subject": "subject",
                "contentType": "Html",
                "fromAddress": "sender@altinnxyz.no",
                "toAddress": "recipient@altinnxyz.no",
                "notificationId": "00000000-0000-0000-0000-000000000001"
            }
            """;

        var command = JsonSerializer.Deserialize<SendEmailCommand>(sendEmailCommandJson, _options);

        Assert.NotNull(command);
        Assert.Equal("body", command.Body);
        Assert.Equal("subject", command.Subject);
        Assert.Equal("Html", command.ContentType);
        Assert.Equal("sender@altinnxyz.no", command.FromAddress);
        Assert.Equal("recipient@altinnxyz.no", command.ToAddress);
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), command.NotificationId);
    }
}
