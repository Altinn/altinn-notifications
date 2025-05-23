using Altinn.Notifications.Core.Models.Delivery;

namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Repository for handling status feed related operations.
    /// </summary>
    public interface IStatusFeedRepository
    {
        /// <summary>
        /// Get status feed
        /// </summary>
        /// <param name="seq">Start sequence id for getting array of status feed entries</param>
        /// <param name="creatorName">Name of service owner</param>
        /// <param name="cancellationToken">Token for cancelling the current request</param>
        /// <param name="limit">Optional parameter for setting the total number of entries returned</param>
        /// <returns>List of status feed entries</returns>
        Task<List<StatusFeed>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken, int limit = 50);
    }
}
