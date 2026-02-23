using System.Text.Json.Nodes;

using Altinn.Notifications.Email.Core.Status;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Core.Status;

public class SendOperationResultTests
{
    private readonly SendOperationResult _operationResult;
    private readonly string _serializedOperationResult;

    public SendOperationResultTests()
    {
        Guid id = Guid.NewGuid();
        _operationResult = new SendOperationResult()
        {
            NotificationId = id,
            OperationId = "operation-id",
            SendResult = EmailSendResult.Sending
        };

        _serializedOperationResult = new JsonObject()
        {
            { "notificationId", id },
            { "operationId",  "operation-id" },
            { "sendResult", "Sending" }
        }.ToJsonString();
    }

    [Fact]
    public void SerializeToJson()
    {
        // Arrange
        string expected = _serializedOperationResult;

        // Act
        var actual = _operationResult.Serialize();

        // Assert
        Assert.Equal(expected, actual);
    }
}
