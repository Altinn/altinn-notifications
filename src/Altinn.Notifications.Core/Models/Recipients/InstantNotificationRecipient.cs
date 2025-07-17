namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents recipient information for an urgent notification delivery.
/// </summary>
public record InstantNotificationRecipient
{
    /// <summary>
    /// The SMS delivery details including recipient, content, and delivery parameters.
    /// </summary>
    public required ShortMessageDeliveryDetails ShortMessageDeliveryDetails { get; init; }
}
