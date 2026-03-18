namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Wolverine command representing a request to send an email notification
/// via Azure Service Bus.
/// </summary>
public sealed class SendEmailCommand
{
    /// <summary>
    /// Gets or sets the notification identifier.
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body of the email.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender address.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient address.
    /// </summary>
    public string ToAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type of the email (e.g. "Plain", "Html").
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}
