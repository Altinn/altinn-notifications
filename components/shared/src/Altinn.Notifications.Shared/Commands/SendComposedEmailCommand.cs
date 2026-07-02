using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send a composed email notification with file attachments.
/// </summary>
public sealed record SendComposedEmailCommand : SendEmailCommand
{
    /// <summary>The file attachments to include in the email.</summary>
    [JsonPropertyName("attachments")]
    public IReadOnlyList<SasFileAttachment> Attachments { get; init; } = [];
}
