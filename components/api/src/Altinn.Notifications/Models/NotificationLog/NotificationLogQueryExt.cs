using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Notifications.Models.NotificationLog;

/// <summary>
/// Represents the query parameters for retrieving notification log entries by Dialogporten identifiers.
/// </summary>
public class NotificationLogQueryExt
{
    /// <summary>
    /// The Dialogporten dialog identifier to filter by.
    /// </summary>
    [BindRequired]
    [FromQuery(Name = "dialogId")]
    [JsonPropertyName("dialogId")]
    public Guid DialogId { get; set; }

    /// <summary>
    /// The Dialogporten transmission identifier to filter by.
    /// </summary>
    [FromQuery(Name = "transmissionId")]
    [JsonPropertyName("transmissionId")]
    public Guid? TransmissionId { get; set; }
}
