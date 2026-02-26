using Altinn.Notifications.Core.Models.Status;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Repository for handling status feed related operations.
/// </summary>
public interface IStatusFeedRepository
{
    /// <summary>
    /// Deletes outdated records from the status feed.
    /// </summary>
    /// <remarks>
    /// This method removes records from the status feed that have exceeded their
    /// retention period of 90 days. It is used to maintain the feed's size and ensure efficient
    /// performance.
    /// </remarks>
    /// <param name="cancellationToken">Token for cancelling the current asynchronous request.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The result contains the number of rows affected.
    /// </returns>
    public Task<int> DeleteOldStatusFeedRecords(CancellationToken cancellationToken);

    /// <summary>
    /// Get status feed
    /// </summary>
    /// <param name="seq">Start sequence id for getting array of status feed entries</param>
    /// <param name="creatorName">Name of service owner</param>
    /// <param name="pageSize">Parameter for setting the total number of entries returned</param>
    /// <param name="cancellationToken">Token for cancelling the current asynchronous request</param>
    /// <returns>List of status feed entries</returns>
    public Task<List<StatusFeed>> GetStatusFeed(long seq, string creatorName, int pageSize, CancellationToken cancellationToken);
}
