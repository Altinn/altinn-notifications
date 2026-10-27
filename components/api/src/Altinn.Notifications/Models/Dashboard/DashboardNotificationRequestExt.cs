using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Base request model for dashboard notification lookups, containing optional date range filters.
/// </summary>
public class DashboardNotificationRequestExt
{
    /// <summary>
    /// Start of the date range (inclusive). Defaults to 7 days ago if not provided.
    /// </summary>
    [FromQuery]
    public DateTime? From { get; set; }

    /// <summary>
    /// End of the date range (exclusive). Defaults to now if not provided.
    /// </summary>
    [FromQuery]
    public DateTime? To { get; set; }
}
