using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send a composed email notification with file attachments.
/// </summary>
public sealed record SendComposedEmailCommand : IEmailCommand
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

    /// <summary>The file attachments to include in the email.</summary>
    [JsonPropertyName("attachments")]
    public IReadOnlyList<SasFileAttachment> Attachments { get; init; } = [];
}
