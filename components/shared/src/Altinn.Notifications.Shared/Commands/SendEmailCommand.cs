using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send an email notification from the Notifications API to the Email service.
/// </summary>
public sealed record SendEmailCommand
{
    /// <summary>
    /// The identifier of the email notification.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; }

    /// <summary>
    /// The subject of the email.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// The body of the email.
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// The sender address.
    /// </summary>
    [JsonPropertyName("fromAddress")]
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>
    /// The recipient address.
    /// </summary>
    [JsonPropertyName("toAddress")]
    public string ToAddress { get; init; } = string.Empty;

    /// <summary>
    /// The content type of the email (e.g. "Plain", "Html").
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;
}
