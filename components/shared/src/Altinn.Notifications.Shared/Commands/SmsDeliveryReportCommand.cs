using System.Text.Json.Serialization;

namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents an SMS delivery report published to the ASB queue by the SMS service
/// and consumed by the API service.
/// </summary>
public sealed record SmsDeliveryReportCommand
{
    /// <summary>
    /// Gets the unique identifier of the SMS notification.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid? NotificationId { get; init; }

    /// <summary>
    /// Gets the reference to the delivery in the SMS gateway.
    /// </summary>
    [JsonPropertyName("gatewayReference")]
    public string GatewayReference { get; init; } = string.Empty;

    /// <summary>
    /// Gets the result of the SMS send operation as a string representation of <c>SmsSendResult</c>.
    /// </summary>
    [JsonPropertyName("sendResult")]
    public string SendResult { get; init; } = string.Empty;
}
