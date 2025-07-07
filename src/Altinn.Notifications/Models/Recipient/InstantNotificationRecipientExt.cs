using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Represents a container for specifying the recipient of a notification order that should be sent instantly.
/// </summary>
public class InstantNotificationRecipientExt
{
    /// <summary>
    /// The recipient information and SMS envelope.
    /// </summary>
    /// <remarks>
    /// Contains the recipient's phone number, the message content,
    /// time-to-live setting, and sender information needed to deliver the SMS.
    /// </remarks>
    [Required]
    [JsonPropertyName("recipientSms")]
    public required RecipientTimedSmsExt RecipientTimedSms { get; init; }
}
