using System.Text.Json;

using Altinn.Notifications.Shared.Commands;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Commands;

public class EmailSendResultCommandSerializationTests
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = false };

    [Fact]
    public void EmailSendResultCommand_Serializes_WithExpectedJsonPropertyNames()
    {
        var command = new EmailSendResultCommand
        {
            OperationId = "op-123",
            SendResult = "Delivered",
            NotificationId = Guid.Empty
        };

        var serializedString = JsonSerializer.Serialize(command, _options);
        var jsonDocument = JsonDocument.Parse(serializedString);
        var jsonElement = jsonDocument.RootElement;

        Assert.True(jsonElement.TryGetProperty("sendResult", out _), "Expected property 'sendResult' not found.");
        Assert.True(jsonElement.TryGetProperty("operationId", out _), "Expected property 'operationId' not found.");
        Assert.True(jsonElement.TryGetProperty("notificationId", out _), "Expected property 'notificationId' not found.");
    }

    [Fact]
    public void EmailSendResultCommand_Deserializes_FromExpectedJsonPropertyNames()
    {
        const string json = """
            {
                "operationId": "op-456",
                "sendResult": "Failed_Bounced",
                "notificationId": "00000000-0000-0000-0000-000000000001"
            }
            """;

        var command = JsonSerializer.Deserialize<EmailSendResultCommand>(json, _options);

        Assert.NotNull(command);
        Assert.Equal("op-456", command.OperationId);
        Assert.Equal("Failed_Bounced", command.SendResult);
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), command.NotificationId);
    }
}
