using System.Collections.Immutable;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Integrations.Kafka.Publishers;

/// <summary>
/// Kafka-based implementation of <see cref="ISendSmsPublisher"/> that publishes
/// SMS notifications to a Kafka topic via <see cref="IKafkaProducer"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KafkaSendSmsPublisher"/> class.
/// </remarks>
/// <param name="kafkaProducer">The Kafka producer used to publish messages.</param>
/// <param name="topicName">The Kafka topic to publish SMS commands to.</param>
internal sealed class KafkaSendSmsPublisher(IKafkaProducer kafkaProducer, string topicName) : ISendSmsPublisher
{
    private readonly IKafkaProducer _kafkaProducer = kafkaProducer;
    private readonly string _topicName = topicName;

    /// <inheritdoc/>
    public async Task<Sms?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool success = await _kafkaProducer.ProduceAsync(_topicName, sms.Serialize());
        return success ? null : sms;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken)
    {
        if (smsList.Count == 0)
        {
            return [];
        }

        var serializedSms = smsList.Select(sms => sms.Serialize()).ToImmutableList();
        var unpublishedSerialized = await _kafkaProducer.ProduceAsync(_topicName, serializedSms, cancellationToken);

        var failedSms = new List<Sms>();
        foreach (var unpublished in unpublishedSerialized)
        {
            var deserialized = JsonSerializer.Deserialize<Sms>(unpublished, JsonSerializerOptionsProvider.Options);
            if (deserialized is not null && deserialized.NotificationId != Guid.Empty)
            {
                failedSms.Add(deserialized);
            }
        }

        return failedSms;
    }
}
