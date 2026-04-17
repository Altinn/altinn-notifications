using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Integrations.Publishers;

/// <summary>
/// Publishes SMS delivery report results to the Kafka status-updated topic.
/// This is the default publisher when <c>EnableSmsDeliveryReportPublisher</c> is <c>false</c>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KafkaSmsDeliveryReportPublisher"/> class.
/// </remarks>
public class KafkaSmsDeliveryReportPublisher(ICommonProducer producer, string topicName) : ISmsDeliveryReportPublisher
{
    private readonly ICommonProducer _producer = producer;
    private readonly string _topicName = topicName;

    /// <inheritdoc/>
    public Task PublishAsync(SendOperationResult result)
        => _producer.ProduceAsync(_topicName, result.Serialize());
}
