namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Defines a model for sending both email and SMS notifications to specific addresses.
/// </summary>
public class RecipientEmailAndSms
{
    /// <summary>
    /// Gets or sets the email address of the intended recipient.
    /// </summary>
    /// <remarks>
    /// This is the destination address where the email will be delivered.
    /// </remarks>
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the recipient's phone number in international format.
    /// </summary>
    /// <remarks>
    /// This is the destination number where the SMS will be delivered.
    /// The phone number should include the country code (e.g., +4799999999).
    /// </remarks>
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the email message.
    /// </summary>
    /// <remarks>
    /// These settings control how and when the email will be composed and delivered to the recipient.
    /// </remarks>
    public required EmailSendingOptions EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the SMS message.
    /// </summary>
    /// <remarks>
    /// Contains sender information, message content, and delivery timing preferences.
    /// </remarks>
    public required SmsSendingOptions SmsSettings { get; set; }
}
