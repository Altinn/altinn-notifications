using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request to create a notification order with one or more reminders.
/// </summary>
public class NotificationOrderWithRemindersRequestExt : NotificationOrderRequestBasePropertiesExt
{
    /// <summary>
    /// Gets or sets the idempotency identifier defined by the sender.
    /// </summary>
    /// <value>
    /// A unique key defined by the sender to ensure idempotency.
    /// </value>
    [Required]
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifiers for one or more dialogs and/or transmissions within Dialogporten.
    /// </summary>
    [JsonPropertyName("dialogportenAssociation")]
    public DialogportenAssociationExt? DialogportenAssociation { get; set; }

    /// <summary>
    /// Gets or sets the associated recipient information.
    /// </summary>
    [Required]
    [JsonPropertyName("recipient")]
    public required RecipientTypesAssociatedWithRequestExt Recipient { get; set; } = new RecipientTypesAssociatedWithRequestExt();

    /// <summary>
    /// Gets or sets the reminders associated with the notification order.
    /// </summary>
    public List<NotificationOrderReminderRequestExt>? Reminders { get; set; }

    /// <summary>
    /// Json serialized the <see cref="EmailNotificationOrderRequestExt"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}
