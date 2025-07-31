using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Service for handling status feed related operations.
/// </summary>
public interface IStatusFeedService
{
    /// <summary>
    /// Get status feed
    /// </summary>
    /// <param name="seq">The exclusive sequence number starting point of the status feed. Sequence ids after this point will be returned</param>
    /// <param name="creatorName">Name of the service owner</param>
    /// <param name="cancellationToken">A CancellationToken for cancelling an ongoing asynchronous Task</param>
    /// <returns>Result object containing, on success: a list of order status objects following the sequence number. On failure: contains a ServiceError object</returns>
    Task<Result<List<StatusFeed>, ServiceError>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes outdated records from the status feed table.
    /// </summary>
    /// <remarks>This method removes records that are no longer relevant based on predefined criteria 90 days since creation. It is
    /// used to maintain the status feed's size and relevance.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task DeleteOldStatusFeedRecords();
}
