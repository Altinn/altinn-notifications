namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents the content and sender information for an email message.
/// </summary>
public record InstantEmailContent
{
    /// <summary>
    /// The sender email address.
    /// </summary>
    public string? FromAddress { get; init; }

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
}
