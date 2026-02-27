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
    /// <param name="topicName">The name of the Kafka topic to publish the message to. Must be a valid, configured topic.</param>
    /// <param name="message">The content of the message to be published. Must not be null, empty or whitespace.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// Returns <c>true</c> if the message was successfully published and persisted; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> ProduceAsync(string topicName, string message);

    /// <summary>
    /// Publishes a batch of messages to the specified Kafka topic with support for partial success handling.
    /// </summary>
    /// <param name="topicName">The name of the Kafka topic to publish the messages to. Must be a valid, configured topic.</param>
    /// <param name="messages">A collection of message payloads to be published. Invalid messages (null, empty, or whitespace) will be excluded from publishing.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation during task scheduling or execution.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// Returns a collection containing both invalid messages and messages that failed to be published.
    /// An empty collection indicates all valid messages were successfully published.
    /// </returns>
    Task<ImmutableList<string>> ProduceAsync(string topicName, ImmutableList<string> messages, CancellationToken cancellationToken = default);
}
