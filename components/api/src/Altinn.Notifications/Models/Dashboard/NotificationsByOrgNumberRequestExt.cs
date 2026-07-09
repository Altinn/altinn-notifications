using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by organization number.
/// </summary>
public class NotificationsByOrgNumberRequestExt : DashboardNotificationRequestExt
{
    /// <summary>
    /// The organization number of the recipient.
    /// </summary>
    [BindRequired]
    [FromHeader(Name = "OrganizationNumber")]
    public required string OrganizationNumber { get; set; }
}
