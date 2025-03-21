using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a reminder notification that can be scheduled to follow an initial notification order.
/// </summary>
/// <remarks>
/// This class enables configuration of follow-up notifications that can be triggered based on
/// specific conditions or time delays after the initial notification. Each reminder can be
/// customized with its own recipient details and timing parameters.
/// </remarks>
public class NotificationReminderRequestExt
{
    /// <summary>
    /// Gets or sets the sender's reference for this reminder.
    /// </summary>
    /// <remarks>
    /// A unique identifier used by the sender to correlate this reminder with their internal systems.
    /// </remarks>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the condition endpoint used to determine if the reminder should be sent.
    /// </summary>
    /// <remarks>
    /// When specified, the system will call this endpoint before sending the reminder.
    /// The reminder will only be sent if the endpoint returns a positive response.
    /// This allows for dynamic decision-making about whether the reminder is still relevant.
    /// </remarks>
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the number of days to delay this reminder.
    /// </summary>
    [Required]
    [DefaultValue(1)]
    [JsonPropertyName("delayDays")]
    public required int DelayDays { get; set; } = 1;

    /// <summary>
    /// Gets or sets the recipient information for this reminder.
    /// </summary>
    /// <remarks>
    /// Specifies the target recipient through one of the supported channels:
    /// email address, SMS number, national identity number, or organization number.
    /// The reminder can be directed to a different recipient than the initial notification.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipient")]
    public required RecipientSpecificationExt Recipient { get; set; }
}
