using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Recipient;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a request to create a composed email order with one or more SAS-referenced files.
/// </summary>
/// <remarks>
/// This request type is email-only. For multi-channel notifications or reminders,
/// use <c>POST notifications/api/v1/future/orders</c> with <see cref="NotificationOrderChainRequestExt"/> instead.
/// </remarks>
public class ComposedEmailRequestExt : NotificationOrderBaseExt
{
    /// <summary>
    /// Optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    /// <remarks>
    /// When specified, associates the notification with a specific dialog or transmission
    /// in Dialogporten, enabling integration between notifications and Dialogporten.
    /// </remarks>
    [JsonPropertyName("dialogportenAssociation")]
    public DialogportenIdentifiersExt? DialogportenAssociation { get; init; }

    /// <summary>
    /// The idempotency identifier defined by the sender.
    /// </summary>
    /// <remarks>
    /// Used to prevent duplicate orders. Submitting the same identifier more than once
    /// returns the original order rather than creating a new one.
    /// </remarks>
    [Required]
    [JsonPropertyName("idempotencyId")]
    public required string IdempotencyId { get; init; }

    /// <summary>
    /// The recipient and sending settings for this composed email order.
    /// </summary>
    [Required]
    [JsonPropertyName("recipient")]
    public required RecipientComposedEmailExt Recipient { get; init; }
}
