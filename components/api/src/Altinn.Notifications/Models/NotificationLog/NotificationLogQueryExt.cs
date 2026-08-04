using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.NotificationLog;

/// <summary>
/// Represents the query parameters for retrieving notification log entries by Dialogporten identifiers.
/// </summary>
public class NotificationLogQueryExt
{
    /// <summary>
    /// The Dialogporten dialog identifier to filter by.
    /// </summary>
    [JsonPropertyName("dialogId")]
    public string? DialogId { get; set; }

    /// <summary>
    /// The Dialogporten transmission identifier to filter by.
    /// </summary>
    [JsonPropertyName("transmissionId")]
    public string? TransmissionId { get; set; }
}
