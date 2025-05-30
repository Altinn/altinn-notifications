using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Service for handling status feed related operations.
    /// </summary>
    public interface IStatusFeedService
    {
        /// <summary>
        /// Get status feed
        /// </summary>
        /// <param name="seq">The sequence number starting point of the status feed</param>
        /// <param name="creatorName">Name of the service owner</param>
        /// <param name="cancellationToken">A CancellationToken for cancelling an ongoing asynchronous Task</param>
        /// <returns>Result object containing, on success: a list of order status objects. On failure: contains a ServiceError object</returns>
        Task<Result<List<StatusFeed>, ServiceError>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken);
    }
}
