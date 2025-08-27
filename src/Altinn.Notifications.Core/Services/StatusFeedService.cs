using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service for handling status feed related operations.
/// </summary>
public class StatusFeedService : IStatusFeedService
{
    private readonly IStatusFeedRepository _statusFeedRepository;
    private readonly StatusFeedConfig _config;
    private readonly ILogger<StatusFeedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusFeedService"/> class.
    /// </summary>
    /// <param name="statusFeedRepository">The repository layer concerned with database integrations</param>
    /// <param name="config">Configuration settings for status feed</param>
    /// <param name="logger">For logging purposes</param>
    public StatusFeedService(
        IStatusFeedRepository statusFeedRepository,
        IOptions<StatusFeedConfig> config,
        ILogger<StatusFeedService> logger)
    {
        _statusFeedRepository = statusFeedRepository;
        _config = config.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task DeleteOldStatusFeedRecords(CancellationToken cancellationToken)
    {
       return _statusFeedRepository.DeleteOldStatusFeedRecords(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<List<StatusFeed>, ServiceError>> GetStatusFeed(int seq, int? pageSize, string creatorName, CancellationToken cancellationToken)
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
            var pageSizeFound = FindPageSize(pageSize);

            var statusFeedEntries = await _statusFeedRepository.GetStatusFeed(
                seq: seq,
                pageSize: pageSizeFound,
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
    
    private int FindPageSize(int? pageSizeUserInput)
    {
        if (!pageSizeUserInput.HasValue)
        {
            return _config.MaxPageSize;
        }

        if (pageSizeUserInput > _config.MaxPageSize)
        {
            return _config.MaxPageSize;
        }

        return pageSizeUserInput.Value;
    }
}
