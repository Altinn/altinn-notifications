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
    public Guid Id { get; set; }

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
    public string RecipientNumber { get; set; }

    /// <summary>
    /// Gets or sets the content of the SMS message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sms"/> class with the specified parameters.
    /// </summary>
    /// <param name="id">The unique identifier of the SMS.</param>
    /// <param name="sender">The sender of the SMS message.</param>
    /// <param name="recipientNumber">The recipient of the SMS message.</param>
    /// <param name="message">The content of the SMS message.</param>
    public Sms(Guid id, string sender, string recipientNumber, string message)
    {
        Id = id;
        Sender = sender;
        Message = message;
        RecipientNumber = recipientNumber;
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
