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
        /// <returns></returns>
        Task<Result<object, ServiceError>> GetStatusFeed(int seq, string creatorName);
    }
}
