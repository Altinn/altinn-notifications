using System.Text.Json;

using Altinn.Notifications.Shared.Commands;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Commands;

public class SmsSendResultCommandSerializationTests
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = false };

    [Fact]
    public void SmsSendResultCommand_Serializes_WithExpectedJsonPropertyNames()
    {
        var command = new SmsSendResultCommand
        {
            SendResult = "Accepted",
            NotificationId = Guid.Empty,
            GatewayReference = "gw-ref-123"
        };

        var serializedString = JsonSerializer.Serialize(command, _options);
        var jsonDocument = JsonDocument.Parse(serializedString);
        var jsonElement = jsonDocument.RootElement;

        Assert.True(jsonElement.TryGetProperty("sendResult", out _), "Expected property 'sendResult' not found.");
        Assert.True(jsonElement.TryGetProperty("gatewayReference", out _), "Expected property 'gatewayReference' not found.");
        Assert.True(jsonElement.TryGetProperty("notificationId", out _), "Expected property 'notificationId' not found.");
    }

    [Fact]
    public void SmsSendResultCommand_WhenGatewayReferenceIsNull_OmitsGatewayReferenceFromJson()
    {
        var command = new SmsSendResultCommand
        {
            GatewayReference = null,
            SendResult = "Failed",
            NotificationId = Guid.Empty
        };

        var serializedString = JsonSerializer.Serialize(command, _options);
        var jsonDocument = JsonDocument.Parse(serializedString);
        var jsonElement = jsonDocument.RootElement;

        Assert.False(jsonElement.TryGetProperty("gatewayReference", out _), "Property 'gatewayReference' should be omitted when null.");
    }

    [Fact]
    public void SmsSendResultCommand_Deserializes_FromExpectedJsonPropertyNames()
    {
        const string json = """
            {
                "sendResult": "Failed_InvalidRecipient",
                "gatewayReference": "LM-REF-00012345",
                "notificationId": "00000000-0000-0000-0000-000000000001"
            }
            """;

        var command = JsonSerializer.Deserialize<SmsSendResultCommand>(json, _options);

        Assert.NotNull(command);
        Assert.Equal("Failed_InvalidRecipient", command.SendResult);
        Assert.Equal("LM-REF-00012345", command.GatewayReference);
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), command.NotificationId);
    }

    [Fact]
    public void SmsSendResultCommand_WhenGatewayReferenceAbsentInJson_DeserializesToNull()
    {
        const string json = """
            {
                "sendResult": "Failed",
                "notificationId": "00000000-0000-0000-0000-000000000001"
            }
            """;

        var command = JsonSerializer.Deserialize<SmsSendResultCommand>(json, _options);

        Assert.NotNull(command);
        Assert.Null(command.GatewayReference);
    }
}
