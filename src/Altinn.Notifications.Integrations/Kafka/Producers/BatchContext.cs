using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates the state and results of a batch message processing operation.
/// </summary>
/// <remarks>
/// This context object follows the Context Object pattern to group related data and 
/// reduce parameter passing between methods during batch processing operations.
/// </remarks>
public record BatchContext
{
    /// <summary>
    /// The name of the Kafka topic for the batch operation.
    /// </summary>
    public string Topic { get; init; } = string.Empty;

    /// <summary>
    /// The collection of valid messages that can be processed.
    /// </summary>
    /// <remarks>
    /// Valid messages are non-null, non-empty, and non-whitespace strings.
    /// </remarks>
    public List<string> ValidMessages { get; init; } = new();

    /// <summary>
    /// The collection of invalid messages that cannot be processed.
    /// </summary>
    /// <remarks>
    /// Invalid messages are null, empty, or contain only whitespace characters.
    /// </remarks>
    public List<string> InvalidMessages { get; init; } = new();

    /// <summary>
    /// The collection of messages that were not successfully published.
    /// </summary>
    /// <remarks>
    /// This collection includes both invalid messages and valid messages that 
    /// failed during the publishing process.
    /// </remarks>
    public List<string> UnpublishedMessages { get; init; } = new();

    /// <summary>
    /// The number of delivery tasks that were successfully scheduled.
    /// </summary>
    /// <remarks>
    /// This count may be less than the total valid messages if cancellation 
    /// occurred during the scheduling phase.
    /// </remarks>
    public int ScheduledCount { get; init; }

    /// <summary>
    /// The number of messages that were successfully published.
    /// </summary>
    /// <remarks>
    /// Success is determined by receiving a <see cref="PersistenceStatus.Persisted"/> 
    /// status from the Kafka delivery result.
    /// </remarks>
    public int SuccessCount { get; init; }

    /// <summary>
    /// The value indicating whether the batch context contains valid 
    /// data and can proceed with processing.
    /// </summary>
    /// <remarks>
    /// This flag is set to false if topic validation fails or if no valid messages are available.
    /// </remarks>
    public bool IsValid { get; init; }
}
