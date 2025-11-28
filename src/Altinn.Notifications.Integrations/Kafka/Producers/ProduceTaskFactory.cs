using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates a deferred factory for producing a single Kafka message, along with the message payload it was created for.
/// </summary>
public sealed record ProduceTaskFactory
{
    /// <summary>
    /// The message this factory will produce to the Kafka topic.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The deferred factory that, when invoked, produces the message and returns the delivery result.
    /// </summary>
    public Func<Task<DeliveryResult<Null, string>>> ProduceTask { get; init; } = default!;
}
