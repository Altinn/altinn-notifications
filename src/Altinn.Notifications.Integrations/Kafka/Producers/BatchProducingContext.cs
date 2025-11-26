using System.Collections.Immutable;

using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates the state and results of a batch message processing operation.
/// </summary>
public sealed record BatchProducingContext
{
    /// <summary>
    /// The collection of valid messages that can be scheduled.
    /// </summary>
    /// <remarks>
    /// Valid messages are non-null, non-empty, and non-whitespace strings.
    /// </remarks>
    public IImmutableList<string> ValidMessages { get; init; } = [];

    /// <summary>
    /// The collection of invalid messages that cannot be scheduled.
    /// </summary>
    /// <remarks>
    /// Invalid messages are <c>null</c>, empty, or contain only whitespace characters.
    /// </remarks>
    public IImmutableList<string> InvalidMessages { get; init; } = [];

    /// <summary>
    /// The messages that were successfully produced to Kafka topic and acknowledged by all brokers.
    /// </summary>
    /// <remarks>
    /// A message is included here only after its corresponding produce task completes without throwing
    /// and the delivery callback returns an acknowledgment (e.g. a <see cref="DeliveryResult{TKey,TValue}"/> with a valid offset).
    /// </remarks>
    public IImmutableList<string> ProducedMessages { get; init; } = [];

    /// <summary>
    /// The messages that were not successfully produced to Kafka topic.
    /// </summary>
    /// <remarks>
    /// This set can include:
    /// 1. Valid messages whose produce task failed (exception, faulted delivery, or negative acknowledgment).
    /// 2. Valid messages intentionally skipped (e.g. short–circuit on prior fatal error or cancellation).
    /// 3. Valid messages whose produce task was never started due to an early exit condition.
    /// Invalid messages are never added here (they appear in <see cref="InvalidMessages"/> instead).
    /// </remarks>
    public IImmutableList<string> NotProducedMessages { get; init; } = [];

    /// <summary>
    /// Valid messages that were scheduled into deferred task factories (index-aligned to <see cref="DeferredProduceTaskFactories"/>).
    /// </summary>
    public IImmutableList<string> ScheduledValidMessages { get; init; } = [];

    /// <summary>
    /// Valid messages that were not scheduled into a deferred task factory due to early cancellation during scheduling.
    /// </summary>
    public IImmutableList<string> UnscheduledValidMessages { get; init; } = [];

    /// <summary>
    /// The deferred task factories used to produce messages to the Kafka topic.
    /// </summary>
    /// <remarks>
    /// Each factory, when invoked, returns a task that produces a single message to the configured topic.
    /// Factories capture the message payload to avoid closure issues with loop variables.
    /// </remarks>
    public IImmutableList<Func<Task<DeliveryResult<Null, string>>>> DeferredProduceTaskFactories { get; init; } = [];
}
