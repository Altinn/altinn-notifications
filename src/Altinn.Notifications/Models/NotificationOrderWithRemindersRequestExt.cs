using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request to create a notification order with one or more reminders.
/// Inherits from <see cref="NotificationOrderRequestBasePropertiesExt"/> for common properties.
/// </summary>
public class NotificationOrderWithRemindersRequestExt : NotificationOrderRequestBasePropertiesExt
{
    /// <summary>
    /// Gets or sets the idempotency identifier defined by the sender.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; set; }

    /// <summary>
    /// Gets or sets optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("dialogportenAssociation")]
    public DialogportenAssociationExt? DialogportenAssociation { get; set; }

    /// <summary>
    /// Gets or sets the required recipient information, whether for mobile number, email-address, national identity, or organization number.
    /// </summary>
    [Required]
    [JsonPropertyOrder(3)]
    [JsonPropertyName("recipient")]
    public required RecipientTypesAssociatedWithRequestExt Recipient { get; set; }

    /// <summary>
    /// Gets or sets a list of reminders that may be triggered under certain conditions after the initial notification has been processed.
    /// </summary>
    [JsonPropertyOrder(4)]
    [JsonPropertyName("reminders")]
    public List<NotificationOrderReminderRequestExt>? Reminders { get; set; }
}
