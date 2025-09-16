using System.ComponentModel.DataAnnotations;

namespace Altinn.Notifications.Core.Models.Status;

/// <summary>
/// Request model for fetching status feed entries
/// </summary>
public class GetStatusFeedRequest
{
    /// <summary>
    /// The sequence number to start fetching status feed entries from
    /// </summary>
    [Range(0, long.MaxValue)]
    public long Seq { get; set; } = 0;

    /// <summary>
    /// The number of items to return in one page. The default value is set by the API
    /// </summary>
    public int? PageSize { get; set; }
}
