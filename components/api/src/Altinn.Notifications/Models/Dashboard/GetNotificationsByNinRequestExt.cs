namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by national identity number.
/// </summary>
public class GetNotificationsByNinRequestExt
{
    /// <summary>
    /// The national identity number of the recipient.
    /// </summary>
    public string Nin { get; set; } = string.Empty;

    /// <summary>
    /// Start of the date range (inclusive). Defaults to 7 days ago if not provided.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// End of the date range (exclusive). Defaults to now if not provided.
    /// </summary>
    public DateTimeOffset? To { get; set; }
}
