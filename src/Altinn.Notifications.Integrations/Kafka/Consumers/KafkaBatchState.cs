using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Represents the state of a Kafka message batch during processing, tracking both input messages and successful outcomes.
/// </summary>
public sealed record KafkaBatchState
{
    /// <summary>
    /// The commit-ready offsets for successfully processed messages. Contains the next offset (original + 1) for each successfully processed message.
    /// </summary>
    public List<TopicPartitionOffset> CommitReadyOffsets { get; init; } = [];

    /// <summary>
    /// The messages retrieved from Kafka during the batch polling operation.
    /// </summary>
    public List<ConsumeResult<string, string>> PolledConsumeResults { get; init; } = [];
}
