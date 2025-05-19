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
        /// <param name="cancellationToken">Token for canceling the current request</param>
        /// <returns></returns>
        Task<List<StatusFeed>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken = default);
    }
}
