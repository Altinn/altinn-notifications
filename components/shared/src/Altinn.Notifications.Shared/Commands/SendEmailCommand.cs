using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send an email notification from the Notifications API to the Email service.
/// </summary>
public sealed record SendEmailCommand : IEmailCommand
{
    /// <inheritdoc/>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; }

    /// <inheritdoc/>
    [JsonPropertyName("subject")]
    public string Subject { get; init; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("fromAddress")]
    public string FromAddress { get; init; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("toAddress")]
    public string ToAddress { get; init; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;
}
