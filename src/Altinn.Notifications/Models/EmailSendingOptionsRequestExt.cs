using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines email configuration settings used in notification requests.
/// </summary>
public class EmailSendingOptionsRequestExt
{
    /// <summary>
    /// Gets or sets the main body content of the email.
    /// </summary>
    /// <remarks>
    /// Contains the primary message content to be delivered to the recipient.
    /// May include plain text or HTML markup depending on the ContentType setting.
    /// </remarks>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the content type (plain text or HTML) of the email.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="EmailContentTypeExt.Plain"/>.
    /// Determines how email clients will render the body content.
    /// </remarks>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("contentType")]
    public EmailContentTypeExt ContentType { get; set; } = EmailContentTypeExt.Plain;

    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    /// <remarks>
    /// This value determines which address will appear as the sender in the recipient's mailbox.
    /// Must be a valid email address format if specified.
    /// </remarks>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("senderEmailAddress")]
    public string? SenderEmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the display name of the sender.
    /// </summary>
    /// <remarks>
    /// Can only be used if <see cref="SenderEmailAddress"/> is set.
    /// Appears alongside the email address in the recipient's email client.
    /// </remarks>
    [JsonPropertyOrder(4)]
    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    /// <summary>
    /// Gets or sets the policy defining when the email should be sent.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SendingTimePolicyExt.Anytime"/> allowing delivery at any time.
    /// </remarks>
    [JsonPropertyOrder(5)]
    [JsonPropertyName("sendingTimePolicy")]
    public SendingTimePolicyExt SendingTimePolicy { get; set; } = SendingTimePolicyExt.Anytime;

    /// <summary>
    /// Gets or sets the subject line of the email.
    /// </summary>
    /// <remarks>
    /// Displayed as the email headline in the recipient's inbox.
    /// </remarks>
    [Required]
    [JsonPropertyOrder(6)]
    [JsonPropertyName("subject")]
    public required string Subject { get; set; }
}
