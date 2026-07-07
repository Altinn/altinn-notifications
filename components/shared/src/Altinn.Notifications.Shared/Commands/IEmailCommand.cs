using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Defines the common properties shared by all email send commands.
/// </summary>
public interface IEmailCommand
{
    /// <summary>
    /// The identifier of the email notification.
    /// </summary>
    [JsonPropertyName("notificationId")]
    Guid NotificationId { get; init; }

    /// <summary>
    /// The subject of the email.
    /// </summary>
    [JsonPropertyName("subject")]
    string Subject { get; init; }

    /// <summary>
    /// The body of the email.
    /// </summary>
    [JsonPropertyName("body")]
    string Body { get; init; }

    /// <summary>
    /// The sender address.
    /// </summary>
    [JsonPropertyName("fromAddress")]
    string FromAddress { get; init; }

    /// <summary>
    /// The recipient address.
    /// </summary>
    [JsonPropertyName("toAddress")]
    string ToAddress { get; init; }

    /// <summary>
    /// The content type of the email (e.g. "Plain", "Html").
    /// </summary>
    [JsonPropertyName("contentType")]
    string ContentType { get; init; }
}
