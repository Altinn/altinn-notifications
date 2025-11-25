using System.Collections.Immutable;

using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates the state and results of a batch message processing operation.
/// </summary>
public record BatchContext
{
    /// <summary>
    /// The number of delivery tasks that were successfully scheduled.
    /// </summary>
    /// <remarks>
    /// This count may be less than the total valid messages if cancellation occurred during the scheduling phase.
    /// </remarks>
    public int ScheduledCount { get; init; }

    /// <summary>
    /// The number of messages that were successfully published.
    /// </summary>
    /// <remarks>
    /// Success is determined by receiving a 
    /// <see cref="PersistenceStatus.Persisted"/> status from the Kafka delivery result.
    /// </remarks>
    public int PublishedCount { get; init; }

    /// <summary>
    /// Indicating whether the batch context contains valid data and can proceed with processing.
    /// </summary>
    /// <remarks>
    /// This flag is set to <c>false</c> if topic validation fails or if no valid messages are available.
    /// </remarks>
    public bool HasValidMessages { get; init; }

    /// <summary>
    /// The name of the Kafka topic for the batch operation.
    /// </summary>
    public string Topic { get; init; } = string.Empty;

    /// <summary>
    /// The valid messages that can be processed.
    /// </summary>
    /// <remarks>
    /// Valid messages are non-null, non-empty, and non-whitespace strings.
    /// </remarks>
    public ImmutableList<string> ValidMessages { get; init; } = [];

    /// <summary>
    /// The invalid messages that cannot be processed.
    /// </summary>
    /// <remarks>
    /// Invalid messages are null, empty, or contain only whitespace characters.
    /// </remarks>
    public ImmutableList<string> InvalidMessages { get; init; } = [];

    /// <summary>
    /// The messages that were not successfully published.
    /// </summary>
    /// <remarks>
    /// This collection includes both invalid messages and valid messages that failed during the publishing phase.
    /// </remarks>
    public ImmutableList<string> UnpublishedMessages { get; init; } = [];
}
