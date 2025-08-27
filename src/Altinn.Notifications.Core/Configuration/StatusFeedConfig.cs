namespace Altinn.Notifications.Core.Configuration;

/// <summary>
/// Configuration class for status feed
/// </summary>
public class StatusFeedConfig
{
    /// <summary>
    /// The maximum number of entries to return in one page
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 1000)]
    public int MaxPageSize { get; set; }
}
