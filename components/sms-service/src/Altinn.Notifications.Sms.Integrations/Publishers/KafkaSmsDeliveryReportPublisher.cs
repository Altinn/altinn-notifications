using Altinn.Notifications.Sms.Core.Configuration;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Integrations.Publishers;

/// <summary>
/// Publishes SMS delivery report results to the Kafka status-updated topic.
/// This is the default publisher when <c>EnableSmsDeliveryReportPublisher</c> is <c>false</c>.
/// </summary>
public class KafkaSmsDeliveryReportPublisher(ICommonProducer producer, TopicSettings settings) : ISmsDeliveryReportPublisher
{
    private readonly ICommonProducer _producer = producer;
    private readonly string _topicName = settings.SmsStatusUpdatedTopicName;

    /// <inheritdoc/>
    public Task PublishAsync(SendOperationResult result)
        => _producer.ProduceAsync(_topicName, result.Serialize());
}
