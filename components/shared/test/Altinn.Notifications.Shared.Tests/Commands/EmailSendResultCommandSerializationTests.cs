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
            SendResult = "Delivered",
            NotificationId = Guid.Empty,
            OperationId = "CBA49321-D68F-4F34-8386-76EAF3B446AA"
        };

        var serializedString = JsonSerializer.Serialize(command, _options);
        var jsonDocument = JsonDocument.Parse(serializedString);
        var jsonElement = jsonDocument.RootElement;

        Assert.True(jsonElement.TryGetProperty("sendResult", out _), "Expected property 'sendResult' not found.");
        Assert.True(jsonElement.TryGetProperty("operationId", out _), "Expected property 'operationId' not found.");
        Assert.True(jsonElement.TryGetProperty("notificationId", out _), "Expected property 'notificationId' not found.");
    }

    [Fact]
    public void EmailSendResultCommand_WhenOperationIdIsNull_OmitsOperationIdFromJson()
    {
        var command = new EmailSendResultCommand
        {
            OperationId = null,
            SendResult = "Failed",
            NotificationId = Guid.Empty
        };

        var serializedString = JsonSerializer.Serialize(command, _options);
        var jsonDocument = JsonDocument.Parse(serializedString);
        var jsonElement = jsonDocument.RootElement;

        Assert.False(jsonElement.TryGetProperty("operationId", out _), "Property 'operationId' should be omitted when null.");
    }

    [Fact]
    public void EmailSendResultCommand_Deserializes_FromExpectedJsonPropertyNames()
    {
        const string json = """
            {
                "sendResult": "Failed_Bounced",
                "operationId": "153821EF-D821-444A-8D54-C0C27CC77689",
                "notificationId": "00000000-0000-0000-0000-000000000001"
            }
            """;

        var command = JsonSerializer.Deserialize<EmailSendResultCommand>(json, _options);

        Assert.NotNull(command);
        Assert.Equal("Failed_Bounced", command.SendResult);
        Assert.Equal("153821EF-D821-444A-8D54-C0C27CC77689", command.OperationId);
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), command.NotificationId);
    }

    [Fact]
    public void EmailSendResultCommand_WhenOperationIdAbsentInJson_DeserializesToNull()
    {
        const string json = """
            {
                "sendResult": "Failed",
                "notificationId": "00000000-0000-0000-0000-000000000001"
            }
            """;

        var command = JsonSerializer.Deserialize<EmailSendResultCommand>(json, _options);

        Assert.NotNull(command);
        Assert.Null(command.OperationId);
    }
}
