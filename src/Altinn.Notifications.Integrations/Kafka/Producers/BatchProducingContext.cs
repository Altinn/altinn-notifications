using System.Collections.Immutable;

using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates the state and results of a message batch processing operation.
/// </summary>
public sealed record BatchProducingContext
{
    /// <summary>
    /// The collection of valid messages that can be scheduled to be produced to the Kafka topic.
    /// </summary>
    /// <remarks>
    /// Valid messages are non-null, non-empty, and non-whitespace strings.
    /// </remarks>
    public ImmutableList<string> ValidMessages { get; init; } = [];

    /// <summary>
    /// The collection of invalid messages that cannot be scheduled to be produced to the Kafka topic.
    /// </summary>
    /// <remarks>
    /// Invalid messages are <c>null</c>, empty, or contain only whitespace characters.
    /// </remarks>
    public ImmutableList<string> InvalidMessages { get; init; } = [];

    /// <summary>
    /// The messages that were successfully produced to the Kafka topic and acknowledged by all brokers.
    /// </summary>
    /// <remarks>
    /// A message is included here only after its corresponding produce task completes without throwing
    /// and the delivery callback returns an acknowledgment (e.g. a <see cref="DeliveryResult{TKey,TValue}"/> with a valid offset).
    /// </remarks>
    public ImmutableList<string> ProducedMessages { get; init; } = [];

    /// <summary>
    /// The messages that were not successfully produced to the Kafka topic.
    /// </summary>
    /// <remarks>
    /// This set includes valid messages that should be retried:
    /// 1. Valid messages whose produce task failed (exception, faulted delivery, or negative acknowledgment).
    /// 2. Valid messages intentionally skipped (e.g. short–circuit on prior or fatal error).
    /// </remarks>
    public ImmutableList<string> NotProducedMessages { get; init; } = [];

    /// <summary>
    /// The deferred task factories used to produce messages to the Kafka topic.
    /// </summary>
    /// <remarks>
    /// Each factory, when invoked, returns a task that produces a single message to the configured topic.
    /// Factories capture the message payload to avoid closure issues with loop variables.
    /// </remarks>
    public ImmutableList<Func<Task<DeliveryResult<Null, string>>>> DeferredProduceTaskFactories { get; init; } = [];
}
