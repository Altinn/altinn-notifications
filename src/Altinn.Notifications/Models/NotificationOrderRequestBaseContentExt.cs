using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents the common properties of notification order request.
/// </summary>
public class NotificationOrderRequestBaseContentExt
{
    /// <summary>
    /// Gets or sets the sender's reference.
    /// </summary>
    /// <value>
    /// A reference used to identify the notification order in the sender's system.
    /// </value>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the associated Email or SMS can be sent at the earliest.
    /// </summary>
    /// <value>
    /// The requested send time, which can be null and defaults to the current date and time.
    /// </value>
    [JsonPropertyName("requestedSendTime")]
    public DateTime RequestedSendTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the condition endpoint used to check the sending condition.
    /// </summary>
    /// <value>
    /// A URI that determines if the associated notifications should be sent based on certain conditions.
    /// </value>
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }
}
