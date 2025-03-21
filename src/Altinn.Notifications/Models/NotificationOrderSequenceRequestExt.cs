using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request to create a notification order with non or more reminders.
/// Inherits the scheduling options from <see cref="NotificationOrderSchedulingExt"/>.
/// </summary>
public class NotificationOrderSequenceRequestExt : NotificationOrderSchedulingExt
{
    /// <summary>
    /// Gets or sets optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    /// <remarks>
    /// When specified, this associates the notification with specific dialogs or transmissions
    /// in the Dialogporten service, enabling integration between notifications and Dialogporten.
    /// </remarks>
    [JsonPropertyName("dialogportenAssociation")]
    public DialogportenReferenceExt? DialogportenAssociation { get; set; }

    /// <summary>
    /// Gets or sets the idempotency identifier defined by the sender.
    /// </summary>
    [Required]
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; set; }

    /// <summary>
    /// Gets or sets the required recipient information for this reminder.
    /// </summary>
    /// <remarks>
    /// Specifies the target recipient through one of the supported channels:
    /// email address, SMS number, national identity number, or organization number.
    /// The reminder can be directed to a different recipient than the initial notification.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipient")]
    public required RecipientSpecificationExt Recipient { get; set; }

    /// <summary>
    /// Gets or sets a list of reminders that may be triggered under certain conditions after the initial notification has been processed.
    /// </summary>
    /// <remarks>
    /// Each reminder can have its own recipient settings, delay period, and triggering conditions.
    /// </remarks>
    [JsonPropertyName("reminders")]
    public List<NotificationReminderRequestExt>? Reminders { get; set; }
}
