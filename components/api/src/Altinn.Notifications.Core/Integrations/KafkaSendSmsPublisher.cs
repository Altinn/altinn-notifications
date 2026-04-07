using System.Text.Json;

using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Implementation of <see cref="ISendSmsPublisher"/> that publishes SMS notifications to a Kafka topic.
/// This publisher is used when Wolverine/Azure Service Bus is not enabled.
/// </summary>
public class KafkaSendSmsPublisher(IKafkaProducer producer, string topicName) : ISendSmsPublisher
{
    private readonly IKafkaProducer _producer = producer;
    private readonly string _topicName = topicName;

    /// <inheritdoc/>
    public async Task<Sms?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        var result = await _producer.ProduceAsync(_topicName, sms.Serialize());

        if (result)
        {
            return null;
        }

        return sms;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken)
    {
        var result = await _producer.ProduceAsync(_topicName, [.. smsList.Select(sms => sms.Serialize())], cancellationToken);
        
        if (result.Count == 0)
        {
            return [];
        }

        List<Sms> failedSms = [];

        foreach (var unpublishedSmsNotification in result)
        {
            var deserializedSmsNotification = JsonSerializer.Deserialize<Sms>(unpublishedSmsNotification, JsonSerializerOptionsProvider.Options);
            if (deserializedSmsNotification == null || deserializedSmsNotification.NotificationId == Guid.Empty)
            {
                continue;
            }

            failedSms.Add(deserializedSmsNotification);
        }

        return failedSms;
    }
}
