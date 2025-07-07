namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Defines a container for specifying the recipient of an instant notification order.
/// </summary>
public class InstantNotificationRecipient
{
    /// <summary>
    /// The SMS recipient information and message content.
    /// </summary>
    /// <remarks>
    /// Contains the destination phone number, message content,
    /// time-to-live setting, and sender information.
    /// </remarks>
    public required RecipientInstantSms RecipientSms { get; init; }
}
