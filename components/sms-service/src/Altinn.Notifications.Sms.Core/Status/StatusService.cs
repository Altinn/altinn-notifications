using Altinn.Notifications.Sms.Core.Dependencies;

using LinkMobility.PSWin.Receiver.Model;

namespace Altinn.Notifications.Sms.Core.Status;

/// <summary>
/// Service for handling status updates
/// </summary>
public class StatusService(ISmsDeliveryReportPublisher deliveryReportPublisher) : IStatusService
{
    private readonly ISmsDeliveryReportPublisher _deliveryReportPublisher = deliveryReportPublisher;

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(DrMessage message)
    {
        var result = new SendOperationResult
        {
            GatewayReference = message.Reference,
            SendResult = SmsSendResultMapper.ParseDeliveryState(message.State)
        };

        await _deliveryReportPublisher.PublishAsync(result);
    }
}
