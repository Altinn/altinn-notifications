using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represent a notification reminder.
/// </summary>
public class ReminderExt
{
    /// <summary>
    /// Gets or sets the sender's reference.
    /// </summary>
    /// <value>
    /// A reference string used to identify the notification order in the sender's system.
    /// </value>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the condition endpoint used to check the sending condition.
    /// </summary>
    /// <value>
    /// A URI that determines if the associated notifications should be sent based on certain conditions.
    /// </value>
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the associated notifications can be sent at the earliest.
    /// </summary>
    /// <value>
    /// The requested send time, which can be null and defaults to the current date and time.
    /// </value>
    [JsonPropertyName("requestedSendTime")]
    public DateTime? RequestedSendTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the delay days.
    /// </summary>
    /// <value>
    /// The  Reminder will be processed on or as soon as possible after the Notification RequestedSendTime + this number of 24-hour increments, in accordance with the selected policy..
    /// </value>
    public int? DelayDays { get; set; }

    /// <summary>
    /// Gets or sets the recipient information.
    /// </summary>
    /// <value>
    /// An object containing information about the recipient.
    /// </value>
    [JsonPropertyName("recipient")]
    public RecipientTypeExt Recipient { get; set; } = new RecipientTypeExt();
}
