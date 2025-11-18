using Confluent.Kafka;

namespace Altinn.Notifications.Integrations.Kafka.Producers;

/// <summary>
/// Encapsulates the results of executing delivery tasks for a batch of messages.
/// </summary>
/// <remarks>
/// This result object contains either completed delivery results or references to 
/// the original tasks, depending on whether the operation completed successfully 
/// or was cancelled.
/// </remarks>
public record DeliveryTaskResult
{
    /// <summary>
    /// A value indicating whether the delivery operation was cancelled.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, <see cref="DeliveryTasks"/> should be used to determine individual 
    /// task completion status. When <c>false</c>, <see cref="DeliveryResults"/> contains 
    /// the complete results from all tasks.
    /// </remarks>
    public bool WasCancelled { get; init; }

    /// <summary>
    /// Gets or sets the array of delivery results from successfully completed operations.
    /// </summary>
    /// <remarks>
    /// This array is populated when all delivery tasks complete without cancellation.
    /// If the operation was cancelled, this array may be empty and <see cref="DeliveryTasks"/> 
    /// should be used instead to check individual task completion status.
    /// </remarks>
    public DeliveryResult<Null, string>[] DeliveryResults { get; init; } = [];

    /// <summary>
    /// Gets or sets the list of delivery tasks that were executed.
    /// </summary>
    /// <remarks>
    /// This list is used when operations are cancelled to check the completion 
    /// status of individual tasks, as some may have completed before cancellation occurred.
    /// </remarks>
    public List<Task<DeliveryResult<Null, string>>> DeliveryTasks { get; init; } = [];
}
