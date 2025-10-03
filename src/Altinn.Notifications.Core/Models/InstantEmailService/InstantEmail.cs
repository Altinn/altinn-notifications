using System.Text.Json;

namespace Altinn.Notifications.Core.Models.InstantEmailService;

/// <summary>
/// Represents a data transfer model for sending instant emails to recipients through the Altinn Notifications Email service.
/// </summary>
public record InstantEmail
{
    /// <summary>
    /// The sender email address.
    /// </summary>
    public required string Sender { get; init; }

    /// <summary>
    /// The recipient's email address.
    /// </summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// The subject of the email.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// The body of the email message.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// The content type of the body (Plain or Html).
    /// </summary>
    public required Enums.EmailContentType ContentType { get; init; }

    /// <summary>
    /// The unique identifier that connects this email to its corresponding notification order.
    /// </summary>
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// Serializes this email to a JSON string for API communication.
    /// </summary>
    /// <returns>A JSON string representation of this email.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }
}
