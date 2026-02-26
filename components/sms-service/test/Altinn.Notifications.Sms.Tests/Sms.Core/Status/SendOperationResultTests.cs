using System.Text.Json.Nodes;

using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Tests.Sms.Core.Status;

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
            GatewayReference = "gateway-reference",
            SendResult = SmsSendResult.Accepted
        };

        _serializedOperationResult = new JsonObject()
        {
            { "notificationId", id },
            { "gatewayReference",  "gateway-reference" },
            { "sendResult", "Accepted" }
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
