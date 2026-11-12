namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines the entry point for processing past due notification orders, including orders being retried.
/// </summary>
/// <remarks>
/// This service picks up a single due order, routes it to the appropriate channel processing service, and persists the result.
/// </remarks>
public interface IOrderProcessingService
{
    /// <summary>
    /// Processes the next ready notification order whose requested send times have passed.
    /// </summary>
    /// <param name="processRetry">A boolean indicating whether to process retry orders. If true, retrieves orders that are being retried; otherwise, retrieves orders past their send time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether any orders were processed.</returns>
    /// <remarks>
    /// This method retrieves an order that is due for processing, processes it through the services matching its notification channel,
    /// and persists the result in the same unit of work. If processing fails, the unit of work is rolled back to a savepoint and the order is set to retry.
    /// </remarks>
    public Task<bool> TryProcessOrder(bool processRetry, CancellationToken cancellationToken = default);
}
