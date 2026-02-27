using LinkMobility.PSWin.Receiver.Model;

namespace Altinn.Notifications.Sms.Core.Status;

/// <summary>
/// Mapper handling parsing to SmsSendResult
/// </summary>
public static class SmsSendResultMapper
{
    /// <summary>
    /// Parse DeliveryState to SmsSendResult, only mapping types that are relevant for our subsscription
    /// </summary>
    /// <param name="deliveryState">Delivery state from Link Mobility</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Throws exception if unknown delivery state</exception>
    public static SmsSendResult ParseDeliveryState(DeliveryState deliveryState)
    {
        return deliveryState switch
        {
            DeliveryState.UNKNOWN => SmsSendResult.Failed,
            DeliveryState.DELIVRD => SmsSendResult.Delivered,
            DeliveryState.EXPIRED => SmsSendResult.Failed_Expired,
            DeliveryState.DELETED => SmsSendResult.Failed_Deleted,
            DeliveryState.UNDELIV => SmsSendResult.Failed_Undelivered,
            DeliveryState.REJECTD => SmsSendResult.Failed_Rejected,
            DeliveryState.FAILED => SmsSendResult.Failed,
            DeliveryState.NULL => SmsSendResult.Failed,
            DeliveryState.BARRED => SmsSendResult.Failed_BarredReceiver,
            _ => throw new ArgumentException($"Unhandled DeliveryState: {deliveryState}"),
        };
    }
}
