using Altinn.Notifications.Core.Models.Delivery;
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
        /// <param name="seq">Starting point of the status feed</param>
        /// <param name="creatorName">Name of the service owner</param>
        /// <param name="cancellationToken">A CancellationToken for cancelling an ongoing asynchronous Task</param>
        /// <returns>Result object containing List of order status objects on success, Contains a ServiceError object on failure</returns>
        Task<Result<List<StatusFeed>, ServiceError>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken);
    }
}
