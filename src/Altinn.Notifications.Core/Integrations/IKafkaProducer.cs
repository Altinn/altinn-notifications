using System.Collections.Immutable;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines the contract for a Kafka producer responsible for publishing messages to Kafka topics.
/// </summary>
public interface IKafkaProducer
{
    /// <summary>
    /// Publishes a single message to the specified Kafka topic.
    /// </summary>
    /// <param name="topic">The name of the Kafka topic to publish the message to.</param>
    /// <param name="message">The content of the message to be published.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// Returns <c>true</c> if the message was successfully published; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> ProduceAsync(string topic, string message);

    /// <summary>
    /// Publishes a batch of messages to the specified Kafka topic.
    /// </summary>
    /// <param name="topic">The name of the Kafka topic to publish the messages to.</param>
    /// <param name="messages">A collection of message payloads to be published.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// Returns a collection of messages that were not successfully published.
    /// </returns>
    Task<IEnumerable<string>> ProduceAsync(string topic, IImmutableList<string> messages, CancellationToken cancellationToken = default);
}
