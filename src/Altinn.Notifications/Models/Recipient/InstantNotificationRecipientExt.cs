using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Defines a container for specifying the recipient of an instant notification order.
/// </summary>
public class InstantNotificationRecipientExt
{
    /// <summary>
    /// The SMS recipient information and message content.
    /// </summary>
    /// <remarks>
    /// Contains the destination phone number, message content,
    /// time-to-live setting, and sender information.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipientSms")]
    public required RecipientInstantSmsExt RecipientSms { get; init; }
}
