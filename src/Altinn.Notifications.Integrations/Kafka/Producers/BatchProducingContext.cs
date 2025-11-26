using System.Collections.Immutable;

using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates the state and results of a batch message processing operation.
/// </summary>
public sealed record BatchProducingContext
{
    /// <summary>
    /// The number of delivery tasks that were successfully scheduled.
    /// </summary>
    /// <remarks>
    /// This count may be less than the total valid messages if cancellation occurred during the scheduling phase.
    /// </remarks>
    public int ScheduledCount { get; init; } = 0;

    /// <summary>
    /// The number of messages that were successfully published.
    /// </summary>
    /// <remarks>
    /// Success is determined by receiving a <see cref="PersistenceStatus.Persisted"/> status from the Kafka delivery result.
    /// </remarks>
    public int PublishedCount { get; init; } = 0;

    /// <summary>
    /// The Kafka topic name targeted by this batch operation.
    /// </summary>
    /// <remarks>
    /// An empty topic indicates topic validation failed or was not provided.
    /// </remarks>
    public string Topic { get; init; } = string.Empty;

    /// <summary>
    /// A value indicating whether this context contains valid, publishable messages and a valid topic.
    /// </summary>
    /// <remarks>
    /// This flag is <c>false</c> if topic validation fails or if no valid messages are available.
    /// </remarks>
    public bool HasValidMessages { get; init; } = false;

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
    /// The collection of messages that were not successfully published.
    /// </summary>
    /// <remarks>
    /// This includes both invalid messages and valid messages that failed during the publishing phase.
    /// </remarks>
    public IImmutableList<string> UnpublishedMessages { get; init; } = [];

    /// <summary>
    /// The deferred task factories used to produce messages to Kafka.
    /// </summary>
    /// <remarks>
    /// Each factory, when invoked, returns a task that produces a single message to the configured topic.
    /// Factories capture the message payload to avoid closure issues with loop variables.
    /// </remarks>
    public IImmutableList<Func<Task<DeliveryResult<Null, string>>>> TaskFactories { get; init; } = [];
}
