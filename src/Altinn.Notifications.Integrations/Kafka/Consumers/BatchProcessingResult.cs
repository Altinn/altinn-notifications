using System.Collections.Concurrent;

using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Consumers;

/// <summary>
/// Represents the result of launching batch processing for Kafka messages, encapsulating
/// processing outcomes, successful offset commits, and failure detection status.
/// </summary>
public sealed class BatchProcessingResult
{
    /// <summary>
    /// The subset of messages from the batch that were actually launched for processing.
    /// </summary>
    public List<ConsumeResult<string, string>> LaunchedMessages { get; }

    /// <summary>
    /// A thread-safe collection of TopicPartitionOffset values representing the next offset 
    /// </summary>
    public ConcurrentBag<TopicPartitionOffset> SuccessfulNextOffsets { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchProcessingResult"/> class.
    /// </summary>
    /// <param name="launchedMessages">The messages that were launched for processing.</param>
    /// <param name="successfulNextOffsets">The offsets of successfully processed messages.</param>
    public BatchProcessingResult(List<ConsumeResult<string, string>> launchedMessages, ConcurrentBag<TopicPartitionOffset> successfulNextOffsets)
    {
        LaunchedMessages = launchedMessages;
        SuccessfulNextOffsets = successfulNextOffsets;
    }
}
