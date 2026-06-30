using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Models.Status;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Service for handling status feed related operations.
/// </summary>
public interface IStatusFeedService
{
    /// <summary>
    /// Get status feed
    /// </summary>
    /// <param name="seq">The exclusive sequence number starting point of the status feed. Sequence ids after this point will be returned. Value 0 with descending order will return the latest entries.</param>
    /// <param name="pageSize">Number of items returned per request</param>
    /// <param name="creatorName">Name of the service owner</param>
    /// <param name="orderBy">The order in which the status feed entries should be returned. The default value is "asc" for ascending order</param>
    /// <param name="cancellationToken">A CancellationToken for cancelling an ongoing asynchronous Task</param>
    /// <returns>A <see cref="Task{TResult}"/> containing a list of order status objects following the sequence number.</returns>
    Task<List<StatusFeed>> GetStatusFeed(long seq, int? pageSize, string creatorName, string orderBy, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes outdated records from the status feed table.
    /// </summary>
    /// <remarks>This method removes records that are no longer relevant based on predefined criteria 90 days since creation. It is
    /// used to maintain the status feed's size and relevance.</remarks>
    /// <param name="cancellationToken">A CancellationToken for cancelling an ongoing asynchronous Task</param>
    /// <returns>A task that represents the asynchronous operation. The result contains the number of rows deleted.</returns>
    public Task<int> DeleteOldStatusFeedRecords(CancellationToken cancellationToken);
}
