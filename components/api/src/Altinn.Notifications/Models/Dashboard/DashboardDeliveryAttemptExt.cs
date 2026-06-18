using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Dashboard;

/// <summary>
/// Represents a single delivery attempt for a dashboard notification, tied to a specific channel.
/// </summary>
public record DashboardDeliveryAttemptExt
{
    /// <summary>
    /// The national identity number of the recipient.
    /// </summary>
    [JsonPropertyName("nationalIdentityNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NationalIdentityNumber { get; init; }

    /// <summary>
    /// The organisation number of the recipient.
    /// </summary>
    [JsonPropertyName("organizationNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrganizationNumber { get; init; }

    /// <summary>
    /// The delivery channel: "email" or "sms".
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// The email address the notification was sent to. Only present when channel is "email".
    /// </summary>
    [JsonPropertyName("emailAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmailAddress { get; init; }

    /// <summary>
    /// The mobile number the notification was sent to. Only present when channel is "sms".
    /// </summary>
    [JsonPropertyName("mobileNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MobileNumber { get; init; }

    /// <summary>
    /// The delivery result status.
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>
    /// When the result was recorded.
    /// </summary>
    [JsonPropertyName("resultTime")]
    public DateTime? ResultTime { get; init; }
}
