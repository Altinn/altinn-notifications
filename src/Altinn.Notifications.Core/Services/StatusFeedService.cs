using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service for handling status feed related operations.
/// </summary>
public class StatusFeedService : IStatusFeedService
{
    private readonly IStatusFeedRepository _statusFeedRepository;
    private readonly NotificationConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusFeedService"/> class.
    /// </summary>
    /// <param name="statusFeedRepository">The repository layer concerned with database integrations</param>
    /// <param name="config">Configuration settings for status feed</param>
    public StatusFeedService(
        IStatusFeedRepository statusFeedRepository,
        IOptions<NotificationConfig> config)
    {
        _statusFeedRepository = statusFeedRepository;
        _config = config.Value;
    }

    /// <inheritdoc />
    public Task DeleteOldStatusFeedRecords(CancellationToken cancellationToken)
    {
       return _statusFeedRepository.DeleteOldStatusFeedRecords(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<List<StatusFeed>>> GetStatusFeed(long seq, int? pageSize, string creatorName, CancellationToken cancellationToken)
    {
        var pageSizeFound = FindPageSize(pageSize);

        return await _statusFeedRepository.GetStatusFeed(
            seq: seq,
            pageSize: pageSizeFound,
            creatorName: creatorName,
            cancellationToken: cancellationToken);
    }
    
    private int FindPageSize(int? pageSizeUserInput)
    {
        int max = _config.StatusFeedMaxPageSize;
        int value = pageSizeUserInput ?? max;
        if (value < 1)
        {
            return 1;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
