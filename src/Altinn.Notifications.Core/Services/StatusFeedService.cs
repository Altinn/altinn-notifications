using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Service for handling status feed related operations.
    /// </summary>
    public class StatusFeedService(IStatusFeedRepository statusFeedRepository) : IStatusFeedService
    {
        /// <summary>
        /// Get status feed
        /// </summary>
        /// <param name="seq">start sequence id for status feed</param>
        /// <param name="creatorName">name of the service owner</param>
        /// <returns>List of status feed entries</returns>
        public async Task<Result<object, ServiceError>> GetStatusFeed(int seq, string creatorName)
        {
            await statusFeedRepository.GetStatusFeed(seq, creatorName);
            return null;
        }
    }
}
