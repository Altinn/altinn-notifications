namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents the request model for sending an SMS to a specific recipient.
/// </summary>
public class SmsRecipientPayload
{
    /// <summary>
    /// Gets or sets the phone number to which the SMS should be delivered.
    /// </summary>
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, including sender number, message content, and sending time policy.
    /// </summary>
    public required SmsRecipientPayloadSettings Settings { get; set; }
}
