using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service for handling status feed related operations.
/// </summary>
public class StatusFeedService : IStatusFeedService
{
    private readonly IStatusFeedRepository _statusFeedRepository;
    private readonly ILogger<StatusFeedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusFeedService"/> class.
    /// </summary>
    /// <param name="statusFeedRepository">The repository layer concerned with database integrations</param>
    /// <param name="logger">For logging purposes</param>
    public StatusFeedService(IStatusFeedRepository statusFeedRepository, ILogger<StatusFeedService> logger)
    {
        _statusFeedRepository = statusFeedRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DeleteOldStatusFeedRecords()
    {
        await _statusFeedRepository.DeleteOldStatusFeedRecords();
    }

    /// <inheritdoc />
    public async Task<Result<List<StatusFeed>, ServiceError>> GetStatusFeed(int seq, string creatorName, CancellationToken cancellationToken)
    {
        if (seq < 0)
        {
            return new ServiceError(400, "Sequence number cannot be less than 0");
        }

        if (string.IsNullOrWhiteSpace(creatorName))
        {
            return new ServiceError(400, "Creator name cannot be null or empty");
        }

        try
        {
            var statusFeedEntries = await _statusFeedRepository.GetStatusFeed(
                seq: seq,
                creatorName: creatorName,
                cancellationToken: cancellationToken);
            return statusFeedEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve status feed");
            return new ServiceError(500, $"Failed to retrieve status feed: {ex.Message}");
        }
    }
}
