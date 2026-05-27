using System.Text.Json.Serialization;

namespace Altinn.Notifications.Tools.DlqManager.Models;

/// <summary>
/// Local copy of the <c>SendSmsCommand</c> contract used as the DLQ message body.
/// Must stay in sync with <c>Altinn.Notifications.Shared/Commands/SendSmsCommand.cs</c>.
/// </summary>
public sealed record SendSmsCommand
{
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; }

    [JsonPropertyName("mobileNumber")]
    public string MobileNumber { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("senderNumber")]
    public string SenderNumber { get; init; } = string.Empty;
}
