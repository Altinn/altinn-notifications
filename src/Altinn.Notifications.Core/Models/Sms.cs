using System.Text.Json;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents an SMS.
/// </summary>
public class Sms
{
    /// <summary>
    /// Gets or sets the unique identifier of the SMS.
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the sender.
    /// </summary>
    /// <remarks>
    /// The sender can be a literal string or a phone number.
    /// </remarks>
    public string Sender { get; set; }

    /// <summary>
    /// Gets or sets the recipient phone number.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the content of the SMS message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sms"/> class with the specified parameters.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the SMS.</param>
    /// <param name="sender">The sender of the SMS message.</param>
    /// <param name="recipient">The recipient of the SMS message.</param>
    /// <param name="message">The content of the SMS message.</param>
    public Sms(Guid notificationId, string sender, string recipient, string message)
    {
        Sender = sender;
        Message = message;
        Recipient = recipient;
        NotificationId = notificationId;
    }

    /// <summary>
    /// Serializes the <see cref="Sms"/> object to a JSON string.
    /// </summary>
    /// <returns>A JSON string representation of the <see cref="Sms"/> object.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }
}
