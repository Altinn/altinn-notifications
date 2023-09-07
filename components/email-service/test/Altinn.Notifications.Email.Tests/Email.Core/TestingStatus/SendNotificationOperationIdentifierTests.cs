using System.Text.Json.Nodes;

using Altinn.Notifications.Email.Core;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Core.Status;

public class SendNotificationOperationIdentifierTests
{
    private readonly Guid _notificationId = Guid.NewGuid();
    private readonly string _serialiedIdentfier;

    public SendNotificationOperationIdentifierTests()
    {
        _serialiedIdentfier = new JsonObject()
            {
                { "notificationId", _notificationId },
                { "operationId", "operation-identifier" },
                {"lastStatusCheck", "1994-06-16T08:00:00Z" }
            }.ToJsonString();
    }

    [Fact]
    public void SerializeToJson()
    {
        // Arrange
        SendNotificationOperationIdentifier identifier = new()
        {
            NotificationId = _notificationId,
            OperationId = "operation-identifier",
            LastStatusCheck = new DateTime(1994, 06, 16, 08, 00, 00, DateTimeKind.Utc)
        };

        string expected = _serialiedIdentfier;

        // Act
        var actual = identifier.Serialize();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryParse_ValidIdentifier_ReturnsTrue()
    {
        bool actualResult = SendNotificationOperationIdentifier.TryParse(_serialiedIdentfier, out SendNotificationOperationIdentifier actual);
        Assert.True(actualResult);
        Assert.Equal("operation-identifier", actual.OperationId);
        Assert.Equal(_notificationId, actual.NotificationId);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        bool actualResult = SendNotificationOperationIdentifier.TryParse(string.Empty, out _);
        Assert.False(actualResult);
    }

    [Fact]
    public void TryParse_InvalidString_ReturnsFalse()
    {
        bool actualResult = SendNotificationOperationIdentifier.TryParse("{\"ticket\":\"noTicket\"}", out _);

        Assert.False(actualResult);
    }

    [Fact]
    public void TryParse_InvalidJsonExceptionThrown_ReturnsFalse()
    {
        bool actualResult = SendNotificationOperationIdentifier.TryParse("{\"ticket:\"noTicket\"}", out _);

        Assert.False(actualResult);
    }
}
