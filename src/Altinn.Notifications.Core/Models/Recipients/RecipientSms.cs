namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents the model for sending an SMS to a specific mobile number.
/// </summary>
public class RecipientSms
{
    /// <summary>
    /// Gets or sets the recipient's phone number in international format.
    /// </summary>
    /// <remarks>
    /// This is the destination number where the SMS will be delivered.
    /// The phone number should include the country code (e.g., +4799999999).
    /// </remarks>
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the SMS message.
    /// </summary>
    /// <remarks>
    /// Contains sender information, message content, and delivery timing preferences.
    /// </remarks>
    public required SmsSendingOptions Settings { get; set; }
}
