namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Query filters for fetching notifications by national identity number.
/// </summary>
public class NotificationsByNinFiltersExt
{
    /// <summary>
    /// Start of the date range (inclusive). Defaults to 7 days ago if not provided.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// End of the date range (exclusive). Defaults to now if not provided.
    /// </summary>
    public DateTime? To { get; set; }
}
