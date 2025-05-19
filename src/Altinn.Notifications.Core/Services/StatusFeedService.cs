using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Service for handling status feed related operations.
    /// </summary>
    public class StatusFeedService : IStatusFeedService
    {
        private readonly IStatusFeedRepository _statusFeedRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusFeedService"/> class.
        /// </summary>
        /// <param name="statusFeedRepository">The repository layer concerned with database integrations</param>
        public StatusFeedService(IStatusFeedRepository statusFeedRepository)
        {
            _statusFeedRepository = statusFeedRepository;
        }

        /// <inheritdoc />
        public async Task<Result<List<StatusFeed>, ServiceError>> GetStatusFeed(int seq, string creatorName)
        {
            var statusFeedEntries = await _statusFeedRepository.GetStatusFeed(seq, creatorName);

            return statusFeedEntries;
        }
    }
}
