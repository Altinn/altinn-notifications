using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.NotificationLog;

/// <summary>
/// Represents the query parameters for retrieving notification log entries by Dialogporten identifiers.
/// At least one of <see cref="DialogId"/> or <see cref="TransmissionId"/> must be provided.
/// </summary>
public class NotificationLogQueryExt
{
    /// <summary>
    /// Gets or sets the Dialogporten dialog identifier to filter by.
    /// </summary>
    [JsonPropertyName("dialogId")]
    public string? DialogId { get; set; }

    /// <summary>
    /// Gets or sets the Dialogporten transmission identifier to filter by.
    /// </summary>
    [JsonPropertyName("transmissionId")]
    public string? TransmissionId { get; set; }
}
