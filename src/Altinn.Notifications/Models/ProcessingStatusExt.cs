using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class ProcessingStatusExt
{
    /// <summary>
    /// Gets or sets the status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    [JsonPropertyName("description")]
    public string StatusDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date time of when the status was last updated
    /// </summary>
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; set; }
}