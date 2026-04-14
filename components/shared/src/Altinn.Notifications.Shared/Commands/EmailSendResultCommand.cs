using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents the outcome of an email send operation, published by the Email service
/// and consumed by the Notifications API via Azure Service Bus.
/// </summary>
public sealed record EmailSendResultCommand
{
    /// <summary>
    /// The unique identifier of the email notification this result belongs to.
    /// A <see langword="null"/> value indicates the notification could not be correlated.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; } = Guid.Empty;

    /// <summary>
    /// The Azure Communication Services operation identifier returned when the email was submitted.
    /// </summary>
    [JsonPropertyName("operationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperationId { get; init; }

    /// <summary>
    /// The terminal send result (e.g. "Delivered", "Failed", "Failed_Bounced").
    /// </summary>
    [JsonPropertyName("sendResult")]
    public string SendResult { get; init; } = string.Empty;
}
