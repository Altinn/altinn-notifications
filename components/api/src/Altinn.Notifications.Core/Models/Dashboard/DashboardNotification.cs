namespace Altinn.Notifications.Core.Models.Dashboard;

/// <summary>
/// Represents a single notification (email or SMS) returned from a dashboard lookup.
/// </summary>
public record DashboardNotification
{
    /// <summary>
    /// The unique identifier for the notification.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// The internal order ID.
    /// </summary>
    public long OrderId { get; init; }

    /// <summary>
    /// The short name of the organisation that created the order.
    /// </summary>
    public string? CreatorName { get; init; }

    /// <summary>
    /// The Altinn resource the notification is related to.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// The sender's reference for the order.
    /// </summary>
    public string? SendersReference { get; init; }

    /// <summary>
    /// When the notification was requested to be sent.
    /// </summary>
    public DateTime RequestedSendTime { get; init; }

    /// <summary>
    /// The recipient's organisation number, if applicable.
    /// </summary>
    public string? RecipientOrgNo { get; init; }

    /// <summary>
    /// The recipient's national identity number.
    /// </summary>
    public string? RecipientNin { get; init; }

    /// <summary>
    /// The delivery channel: "email" or "sms".
    /// </summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// The delivery result status.
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// When the result was recorded.
    /// </summary>
    public DateTime? ResultTime { get; init; }
}
