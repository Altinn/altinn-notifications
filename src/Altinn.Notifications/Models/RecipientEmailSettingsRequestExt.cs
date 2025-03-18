using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents configuration settings that are associated with the request model for sending an E-mail to a specific recipient.
/// </summary>
public class RecipientEmailSettingsRequestExt
{
    /// <summary>
    /// Gets or sets the main body content of the email.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the content type (plain text or HTML) of the email.
    /// Defaults to <see cref="EmailContentTypeExt.Plain"/>.
    /// </summary>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("contentType")]
    public EmailContentTypeExt ContentType { get; set; } = EmailContentTypeExt.Plain;

    /// <summary>
    /// Gets or sets the sender's email address. This value determines
    /// which address will appear as the sender in the recipient's mailbox.
    /// </summary>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("senderEmailAddress")]
    public string? SenderEmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the display name of the sender.
    /// Can only be used if <see cref="SenderEmailAddress"/> is set.
    /// </summary>
    [JsonPropertyOrder(4)]
    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    /// <summary>
    /// Gets or sets the policy defining when the email should be sent.
    /// Defaults to <see cref="SendingTimePolicyExt.WorkingDaysDaytime"/>.
    /// </summary>
    [JsonPropertyOrder(5)]
    [JsonPropertyName("sendingTimePolicy")]
    public SendingTimePolicyExt SendingTimePolicy { get; set; } = SendingTimePolicyExt.Anytime;

    /// <summary>
    /// Gets or sets the subject line of the email.
    /// </summary>
    [Required]
    [JsonPropertyOrder(6)]
    [JsonPropertyName("subject")]
    public required string Subject { get; set; }
}
