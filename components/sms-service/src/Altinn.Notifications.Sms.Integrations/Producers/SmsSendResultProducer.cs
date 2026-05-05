using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;
using Altinn.Notifications.Sms.Integrations.Configuration;

namespace Altinn.Notifications.Sms.Integrations.Producers;

/// <summary>
/// Kafka-based implementation of <see cref="ISmsSendResultDispatcher"/> that publishes
/// terminal SMS send operation results to a configured Kafka topic.
/// This implementation is used when the Azure Service Bus transport is disabled.
/// </summary>
public class SmsSendResultProducer : ISmsSendResultDispatcher
{
    private readonly ICommonProducer _producer;
    private readonly string _topicName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsSendResultProducer"/> class.
    /// </summary>
    public SmsSendResultProducer(ICommonProducer producer, KafkaSettings settings)
    {
        _producer = producer;
        _topicName = settings.SmsStatusUpdatedTopicName;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(SendOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        bool success = await _producer.ProduceAsync(_topicName, result.Serialize());
        if (!success)
        {
            throw new InvalidOperationException("Failed to publish SMS send result.");
        }
    }
}
