using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Notifications.Email.Core.Sending;

namespace Altinn.Notifications.Email.Models.InstantEmail;

/// <summary>
/// Represents a request model for sending an email to a single recipient instantly.
/// </summary>
public record InstantEmailRequest
{
    /// <summary>
    /// The sender address of the email.
    /// </summary>
    [Required]
    [JsonPropertyName("sender")]
    public required string Sender { get; init; }

    /// <summary>
    /// The recipient email address where the email will be sent.
    /// </summary>
    [Required]
    [EmailAddress]
    [JsonPropertyName("recipient")]
    public required string Recipient { get; init; }

    /// <summary>
    /// The subject of the email.
    /// </summary>
    [Required]
    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    /// <summary>
    /// The body content of the email.
    /// </summary>
    [Required]
    [JsonPropertyName("body")]
    public required string Body { get; init; }

    /// <summary>
    /// The content type of the email (Html or Plain).
    /// </summary>
    [Required]
    [JsonPropertyName("contentType")]
    public required EmailContentType ContentType { get; init; }

    /// <summary>
    /// The unique identifier for this specific email notification.
    /// </summary>
    [Required]
    [JsonPropertyName("notificationId")]
    public required Guid NotificationId { get; init; }
}
