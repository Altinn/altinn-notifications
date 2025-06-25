namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the outcome of processing a notification order.
/// </summary>
public record NotificationOrderProcessingResult
{
    /// <summary>
    /// Gets a value indicating whether the order processing failed and should be retried.
    /// </summary>
    /// <value>
    /// <c>true</c> if the processing failed due to a transient issue and the order should be 
    /// enqueued for a retry; <c>false</c> if the processing was successful or failed with a non-retryable error.
    /// </value>
    public bool IsRetryRequired { get; init; }
}
