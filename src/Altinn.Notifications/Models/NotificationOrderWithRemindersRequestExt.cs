using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request to create a notification order with one or more reminders.
/// </summary>
public class NotificationOrderWithRemindersRequestExt
{
    /// <summary>
    /// Gets or sets the idempotency identifier defined by the sender.
    /// </summary>
    /// <value>
    /// A unique key defined by the sender to ensure idempotency.
    /// </value>
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; set; }

    /// <summary>
    /// Gets or sets the identifiers for one or more dialogs and/or transmissions in the Dialogporten.
    /// </summary>
    /// <value>
    /// An object that links one or more dialogs and/or transmissions in the Dialogporten.
    /// This association helps in tracking the dialogs and transmissions related to the notification order.
    /// </value>
    [JsonPropertyName("dialogportenAssociation")]
    public DialogportenAssociationExt? DialogportenAssociation { get; set; }

    /// <summary>
    /// Gets or sets the sender's reference.
    /// </summary>
    /// <value>
    /// A reference string used to identify the notification order in the sender's system.
    /// </value>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the associated notifications can be sent at the earliest.
    /// </summary>
    /// <value>
    /// The requested send time, which can be null and defaults to the current date and time.
    /// </value>
    [JsonPropertyName("requestedSendTime")]
    public DateTime? RequestedSendTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the condition endpoint used to check the sending condition.
    /// </summary>
    /// <value>
    /// A URI that determines if the associated notifications should be sent based on certain conditions.
    /// </value>
    [JsonPropertyName("conditionEndpoint")]
    public Uri? ConditionEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the recipient information.
    /// </summary>
    /// <value>
    /// An object containing information about the recipient.
    /// </value>
    [JsonPropertyName("recipient")]
    public RecipientTypelExt Recipient { get; set; } = new RecipientTypelExt();
}
