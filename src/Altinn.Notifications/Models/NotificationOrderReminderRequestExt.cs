using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a reminder that can be sent following an initial notification order,
/// inheriting common properties from <see cref="NotificationOrderRequestBasePropertiesExt"/>.
/// </summary>
public class NotificationOrderReminderRequestExt : NotificationOrderRequestBasePropertiesExt
{
    /// <summary>
    /// Gets or sets the optional number of days to delay.
    /// The reminder will be processed on or after (RequestedSendTime + DelayDays).
    /// </summary>
    [JsonPropertyOrder(1)]
    public int? DelayDays { get; set; }

    /// <summary>
    /// Gets or sets the required recipient information, whether for mobile number, email-address, national identity, or organization number.
    /// </summary>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("recipient")]
    public required RecipientTypesAssociatedWithRequestExt Recipient { get; set; } = new();
}
