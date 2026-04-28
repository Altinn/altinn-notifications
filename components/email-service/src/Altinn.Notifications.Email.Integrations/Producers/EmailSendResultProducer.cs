using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Configuration;

namespace Altinn.Notifications.Email.Integrations.Producers;

/// <summary>
/// Kafka-based implementation of <see cref="IEmailSendResultDispatcher"/> that publishes
/// terminal email send operation results to a configured Kafka topic.
/// This implementation is used when the Azure Service Bus transport is disabled.
/// </summary>
public class EmailSendResultProducer : IEmailSendResultDispatcher
{
    private readonly ICommonProducer _producer;
    private readonly string _topicName;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendResultProducer"/> class.
    /// </summary>
    public EmailSendResultProducer(ICommonProducer producer, KafkaSettings settings)
    {
        _producer = producer;
        _topicName = settings.EmailStatusUpdatedTopicName;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(SendOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        bool success = await _producer.ProduceAsync(_topicName, result.Serialize());
        if (!success)
        {
            throw new InvalidOperationException("Failed to publish email send result.");
        }
    }
}
