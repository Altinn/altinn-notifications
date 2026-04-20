using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents the outcome of an SMS send operation, published by the SMS service
/// and consumed by the Notifications API via Azure Service Bus.
/// </summary>
public sealed record SmsSendResultCommand
{
    /// <summary>
    /// The unique identifier of the SMS notification this result belongs to.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; init; } = Guid.Empty;

    /// <summary>
    /// The reference to the send attempt in the SMS gateway (e.g. LinkMobility).
    /// </summary>
    [JsonPropertyName("gatewayReference")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GatewayReference { get; init; }

    /// <summary>
    /// The terminal send result (e.g. "Accepted", "Failed", "Failed_InvalidRecipient").
    /// </summary>
    [JsonPropertyName("sendResult")]
    public string SendResult { get; init; } = string.Empty;
}
