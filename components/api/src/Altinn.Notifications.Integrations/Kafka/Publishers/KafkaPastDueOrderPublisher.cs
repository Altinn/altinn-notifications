using System.Collections.Immutable;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Integrations.Kafka.Publishers;

/// <summary>
/// Kafka-based implementation of <see cref="IPastDueOrderPublisher"/> that publishes
/// past-due notification orders to a Kafka topic via <see cref="IKafkaProducer"/>.
/// </summary>
/// <param name="kafkaProducer">The Kafka producer used to publish messages.</param>
/// <param name="topicName">The Kafka topic to publish past-due order commands to.</param>
internal sealed class KafkaPastDueOrderPublisher(IKafkaProducer kafkaProducer, string topicName) : IPastDueOrderPublisher
{
    private readonly IKafkaProducer _kafkaProducer = kafkaProducer;
    private readonly string _topicName = topicName;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationOrder>> PublishAsync(
        IReadOnlyList<NotificationOrder> orders,
        CancellationToken cancellationToken = default)
    {
        if (orders.Count == 0)
        {
            return [];
        }

        var serializedOrders = orders.Select(o => o.Serialize()).ToImmutableList();
        var unpublishedSerialized = await _kafkaProducer.ProduceAsync(_topicName, serializedOrders, cancellationToken);

        var failed = new List<NotificationOrder>();
        foreach (var unpublished in unpublishedSerialized)
        {
            var deserialized = JsonSerializer.Deserialize<NotificationOrder>(unpublished, JsonSerializerOptionsProvider.Options);
            if (deserialized is not null && deserialized.Id != Guid.Empty)
            {
                var original = orders.FirstOrDefault(o => o.Id == deserialized.Id);
                if (original is not null)
                {
                    failed.Add(original);
                }
            }
        }

        return failed;
    }
}
