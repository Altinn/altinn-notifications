using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Request model for fetching notifications by national identity number.
/// </summary>
public class NotificationsByNinRequestExt
{
    /// <summary>
    /// The national identity number of the recipient.
    /// </summary>
    [BindRequired]
    [FromHeader(Name = "NationalIdentityNumber")]
    public required string NationalIdentityNumber { get; set; }

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
