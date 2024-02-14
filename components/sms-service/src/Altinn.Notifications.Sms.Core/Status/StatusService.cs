using Altinn.Notifications.Sms.Core.Configuration;
using Altinn.Notifications.Sms.Core.Dependencies;
using LinkMobility.PSWin.Receiver.Model;

namespace Altinn.Notifications.Sms.Core.Status;

/// <summary>
/// Service for handling status updates
/// </summary>
public class StatusService : IStatusService
{
    private readonly TopicSettings _settings;
    private readonly ICommonProducer _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusService"/> class.
    /// </summary>
    public StatusService(ICommonProducer producer, TopicSettings settings)
    {
        _settings = settings;
        _producer = producer;
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(DrMessage message)
    {
        SendOperationResult result = new()
        {
            GatewayReference = message.Reference,
            SendResult = SmsSendResultMapper.ParseDeliveryState(message.State)
        };
        await _producer.ProduceAsync(_settings.SmsStatusUpdatedTopicName, result.Serialize());
    }
}
