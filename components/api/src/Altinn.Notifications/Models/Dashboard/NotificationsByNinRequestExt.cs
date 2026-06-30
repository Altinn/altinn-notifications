using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by national identity number.
/// </summary>
public class NotificationsByNinRequestExt : DashboardNotificationRequestExt
{
    /// <summary>
    /// The national identity number of the recipient.
    /// </summary>
    [BindRequired]
    [FromHeader(Name = "NationalIdentityNumber")]
    public required string NationalIdentityNumber { get; set; }
}
