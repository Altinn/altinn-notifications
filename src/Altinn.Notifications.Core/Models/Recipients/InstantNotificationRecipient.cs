namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a container for specifying the recipient of a notification order that should be sent instantly.
/// </summary>
public class InstantNotificationRecipient
{
    /// <summary>
    /// The recipient information and SMS envelope.
    /// </summary>
    /// <remarks>
    /// Contains the recipient's phone number, the message content,
    /// time-to-live setting, and sender information needed to deliver the SMS.
    /// </remarks>
    public required RecipientTimedSms RecipientSms { get; init; }
}
