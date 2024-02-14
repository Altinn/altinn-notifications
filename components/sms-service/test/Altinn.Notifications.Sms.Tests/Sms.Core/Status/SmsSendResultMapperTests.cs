using Altinn.Notifications.Sms.Core.Status;
using LinkMobility.PSWin.Receiver.Model;

namespace Altinn.Notifications.Sms.Tests.Sms.Core.Status;

public class SmsSendResultMapperTests
{
    [Theory]
    [InlineData(DeliveryState.UNKNOWN, SmsSendResult.Failed)]
    [InlineData(DeliveryState.DELIVRD, SmsSendResult.Delivered)]
    [InlineData(DeliveryState.EXPIRED, SmsSendResult.Failed_Expired)]
    [InlineData(DeliveryState.DELETED, SmsSendResult.Failed_Deleted)]
    [InlineData(DeliveryState.UNDELIV, SmsSendResult.Failed_Undelivered)]
    [InlineData(DeliveryState.REJECTD, SmsSendResult.Failed_Rejected)]
    [InlineData(DeliveryState.FAILED, SmsSendResult.Failed)]
    [InlineData(DeliveryState.NULL, SmsSendResult.Failed)]
    [InlineData(DeliveryState.BARRED, SmsSendResult.Failed_BarredReceiver)]
    public void ParseDeliveryState_WithValidInput_ReturnsExpectedResult(DeliveryState input, SmsSendResult expected)
    {
        // Act
        var result = SmsSendResultMapper.ParseDeliveryState(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseDeliveryState_WithUnhandledState_ThrowsArgumentException()
    {
        // Arrange
        var unhandledState = (DeliveryState)999;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SmsSendResultMapper.ParseDeliveryState(unhandledState));
    }
}
