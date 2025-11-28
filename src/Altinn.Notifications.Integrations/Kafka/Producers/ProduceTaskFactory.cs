using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates a deferred factory for producing a single Kafka message.
/// </summary>
public sealed record ProduceTaskFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="ProduceTaskFactory"/> with a message and topic-specific producer.
    /// </summary>
    /// <param name="topicName">The Kafka topic name.</param>
    /// <param name="message">The message payload to produce.</param>
    /// <param name="producer">The Kafka producer instance.</param>
    /// <returns>A new <see cref="ProduceTaskFactory"/> instance.</returns>
    public static ProduceTaskFactory Create(string topicName, string message, IProducer<Null, string> producer)
    {
        return new ProduceTaskFactory
        {
            Message = message,
            ProduceTask = () => producer.ProduceAsync(topicName, new Message<Null, string> { Value = message })
        };
    }

    /// <summary>
    /// The message this factory will produce to the Kafka topic.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The deferred factory that, when invoked, produces the message and returns the delivery result.
    /// </summary>
    public Func<Task<DeliveryResult<Null, string>>> ProduceTask { get; init; } = default!;
}
