namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Interface for handling all producer actions for Kafka
/// </summary>
public interface IKafkaProducer
{
    /// <summary>
    /// Produces a message on the provided Kafka topic 
    /// </summary>
    /// <param name="topic">The topic to post a message to</param>
    /// <param name="message">The message to post</param>
    Task<bool> ProduceAsync(string topic, string message);

    /// <summary>
    /// Produces a single message and awaits broker acknowledgment (acks=all).
    /// Use when the caller must know persistence outcome immediately.
    /// </summary>
    /// <param name="topic">Kafka topic name.</param>
    /// <param name="message">Message payload (UTF-8 string).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if persisted on all replicas; otherwise <c>false</c>.</returns>
    Task<bool> ProduceAsync(string topic, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a batch of messages with per-message delivery awaits.
    /// Prefer when you already have multiple messages and want fewer async state machine transitions.
    /// </summary>
    /// <param name="topic">Kafka topic name.</param>
    /// <param name="messages">Collection of message payloads.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of messages successfully persisted.</returns>
    Task<int> ProduceBatchAsync(string topic, IEnumerable<string> messages, CancellationToken cancellationToken = default);
}
