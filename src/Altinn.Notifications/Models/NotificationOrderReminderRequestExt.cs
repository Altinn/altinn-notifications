using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represent a notification reminder.
/// </summary>
public class NotificationOrderReminderRequestExt : NotificationOrderRequestBasePropertiesExt
{
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
    public RecipientTypesAssociatedWithRequestExt Recipient { get; set; } = new RecipientTypesAssociatedWithRequestExt();
}
