using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents the result of creating a notification order using <see cref="NotificationOrderChainRequestExt"/>.
/// </summary>
public class NotificationOrderCreationResultExt : NotificationOrderResponseBaseContentExt
{
    /// <summary>
    /// Gets or sets the reminders associated with this notification order.
    /// </summary>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("reminders")]
    public List<NotificationOrderResponseBaseContentExt>? Reminders { get; set; }
}
