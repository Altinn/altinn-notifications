using System.Text.Json;

using Altinn.Notifications.Shared.Commands;

using Xunit;

namespace Altinn.Notifications.Shared.Tests.Commands;

public class EmailServiceRateLimitCommandSerializationTests
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = false };

    [Fact]
    public void EmailServiceRateLimitCommand_Serializes_WithExpectedJsonPropertyNames()
    {
        var command = new EmailServiceRateLimitCommand
        {
            Data = "{}",
            Source = "platform-notifications-email"
        };

        var json = JsonSerializer.Serialize(command, _options);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.True(root.TryGetProperty("data", out _), "Expected property 'data' not found.");
        Assert.True(root.TryGetProperty("source", out _), "Expected property 'source' not found.");
    }

    [Fact]
    public void EmailServiceRateLimitCommand_Deserializes_FromExpectedJsonPropertyNames()
    {
        const string json = """
            {
                "data": "{}",
                "source": "platform-notifications-email"
            }
            """;

        var command = JsonSerializer.Deserialize<EmailServiceRateLimitCommand>(json, _options);

        Assert.NotNull(command);
        Assert.Equal("{}", command.Data);
        Assert.Equal("platform-notifications-email", command.Source);
    }

    [Fact]
    public void EmailServiceRateLimitCommand_WhenDeserializedFromEmptyJson_UsesDefaultValues()
    {
        const string json = "{}";

        var command = JsonSerializer.Deserialize<EmailServiceRateLimitCommand>(json, _options);

        Assert.NotNull(command);
        Assert.Equal(string.Empty, command.Data);
        Assert.Equal(string.Empty, command.Source);
    }
}
