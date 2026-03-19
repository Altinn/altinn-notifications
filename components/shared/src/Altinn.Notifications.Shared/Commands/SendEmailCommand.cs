using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send an email notification from the Notifications API to the Email service.
/// </summary>
public sealed class SendEmailCommand
{
    /// <summary>
    /// Gets or sets the notification identifier.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body of the email.
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender address.
    /// </summary>
    [JsonPropertyName("fromAddress")]
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient address.
    /// </summary>
    [JsonPropertyName("toAddress")]
    public string ToAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type of the email (e.g. "Plain", "Html").
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;
}
