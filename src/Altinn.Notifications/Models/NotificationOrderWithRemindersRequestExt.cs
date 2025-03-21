﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request to create a notification order with one or more reminders.
/// Inherits the scheduling options from <see cref="NotificationOrderRequestSchedulingExt"/>.
/// </summary>
public class NotificationOrderWithRemindersRequestExt : NotificationOrderRequestSchedulingExt
{
    /// <summary>
    /// Gets or sets the idempotency identifier defined by the sender.
    /// </summary>
    [Required]
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; set; }

    /// <summary>
    /// Gets or sets the required recipient information, whether for mobile number, email-address, national identity, or organization number.
    /// </summary>
    /// <remarks>
    /// Specifies exactly one recipient type for the notification. The recipient information
    /// determines the delivery channel and addressing details.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipient")]
    public required RecipientSpecificationRequestExt Recipient { get; set; }

    /// <summary>
    /// Gets or sets a list of reminders that may be triggered under certain conditions after the initial notification has been processed.
    /// </summary>
    /// <remarks>
    /// Each reminder can have its own recipient settings, delay period, and triggering conditions.
    /// </remarks>
    [JsonPropertyName("reminders")]
    public List<NotificationOrderReminderRequestExt>? Reminders { get; set; }

    /// <summary>
    /// Gets or sets optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    /// <remarks>
    /// When specified, this associates the notification with specific dialogs or transmissions
    /// in the Dialogporten service, enabling integration between notifications and Dialogporten.
    /// </remarks>
    [JsonPropertyName("dialogportenAssociation")]
    public DialogportenReferenceRequestExt? DialogportenAssociation { get; set; }
}
