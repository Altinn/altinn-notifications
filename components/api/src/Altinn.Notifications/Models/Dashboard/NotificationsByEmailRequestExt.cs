using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by email address.
/// </summary>
public class NotificationsByEmailRequestExt : DashboardNotificationRequestExt
{
    /// <summary>
    /// The email address of the recipient.
    /// </summary>
    [BindRequired]
    [FromHeader(Name = "Email")]
    public required string Email { get; set; }
}
