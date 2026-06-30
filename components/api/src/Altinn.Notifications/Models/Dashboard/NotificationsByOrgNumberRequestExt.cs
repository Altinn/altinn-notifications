using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by Organization Number.
/// </summary>
public class NotificationsByOrgNumberRequestExt
{
    /// <summary>
    /// The organization number of the recipient.
    /// </summary>
    [BindRequired]
    [FromHeader(Name = "OrganizationNumber")]
    public required string OrganizationNumber { get; set; }

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
