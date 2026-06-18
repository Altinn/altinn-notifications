namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Combined model used for validating a notifications-by-NIN lookup.
/// </summary>
public class NotificationsByNinRequestExt
{
    /// <summary>
    /// The national identity number of the recipient.
    /// </summary>
    public string Nin { get; set; } = string.Empty;

    /// <summary>
    /// Start of the date range (inclusive). Defaults to 7 days ago if not provided.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// End of the date range (exclusive). Defaults to now if not provided.
    /// </summary>
    public DateTime? To { get; set; }
}
