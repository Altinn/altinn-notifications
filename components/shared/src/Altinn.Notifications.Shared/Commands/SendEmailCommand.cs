using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send an email notification from the Notifications API to the Email service.
/// </summary>
public sealed record SendEmailCommand
{
    /// <summary>
    /// Gets the notification identifier.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; }

    /// <summary>
    /// Gets the subject of the email.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// Gets the body of the email.
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sender address.
    /// </summary>
    [JsonPropertyName("fromAddress")]
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>
    /// Gets the recipient address.
    /// </summary>
    [JsonPropertyName("toAddress")]
    public string ToAddress { get; init; } = string.Empty;

    /// <summary>
    /// Gets the content type of the email (e.g. "Plain", "Html").
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;
}
