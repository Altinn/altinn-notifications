using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services;

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
    public async Task<Result<List<StatusFeed>, ServiceError>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(creatorName))
        {
            return new ServiceError(400, "Missing creator");
        }

        var statusFeedEntries = await _statusFeedRepository.GetStatusFeed(seq: seq, creatorName: creatorName, cancellationToken: cancellationToken);

        return statusFeedEntries;
    }
}
