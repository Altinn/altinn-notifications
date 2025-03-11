using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents the settings required to create an email notification.
/// These settings are populated from the request model used to generate a notification with associated reminders.
/// </summary>
public class EmailRequestSettingsExt
{
    /// <summary>
    /// Gets or sets the body content.
    /// </summary>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("contentType")]
    public EmailContentTypeExt ContentType { get; set; } = EmailContentTypeExt.Plain;

    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("senderEmailAddress")]
    public string? SenderEmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the sender's name.
    /// </summary>
    /// <value>
    /// If set, this value is used as the sender email display name.
    /// Only applicable if the <see cref="SenderEmailAddress"/> is set. It cannot be used on its own.
    /// </value>
    [JsonPropertyOrder(4)]
    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    /// <summary>
    /// Gets or sets the sending time policy for when the email should be sent.
    /// </summary>
    [JsonPropertyOrder(5)]
    [JsonPropertyName("sendingTimePolicy")]
    public SendingTimePolicyExt SendingTimePolicy { get; set; } = SendingTimePolicyExt.WorkingDaysDaytime;

    /// <summary>
    /// Gets or sets the subject line of email.
    /// </summary>
    [Required]
    [JsonPropertyOrder(6)]
    [JsonPropertyName("subject")]
    public required string Subject { get; set; }
}
