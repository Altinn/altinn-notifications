using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by phone number.
/// </summary>
public class NotificationsByPhoneNumberRequestExt : DashboardNotificationRequestExt
{
    /// <summary>
    /// The phone number of the recipient.
    /// </summary>
    [BindRequired]
    [FromHeader(Name = "PhoneNumber")]
    public required string PhoneNumber { get; set; }
}
