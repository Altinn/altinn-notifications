using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.NotificationTemplate;

/// <summary>
/// Represents a template for an email notification.
/// </summary>
public class EmailTemplate : INotificationTemplate
{
    /// <summary>
    /// Gets the body of the email.
    /// </summary>
    public string Body { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the content type of the email.
    /// </summary>
    public EmailContentType ContentType { get; internal set; }

    /// <summary>
    /// Gets the sender address of the email.
    /// </summary>
    public string FromAddress { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the subject of the email.
    /// </summary>
    public string Subject { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the type of the notification template.
    /// </summary>
    /// <value>
    /// The type of the notification template, represented by the <see cref="NotificationTemplateType"/> enum.
    /// </value>
    public NotificationTemplateType Type { get; } = NotificationTemplateType.Email;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailTemplate"/> class with the specified from address, subject, body, and content type.
    /// </summary>
    /// <param name="fromAddress">The sender address of the email. If null, an empty string is used.</param>
    /// <param name="subject">The subject of the email.</param>
    /// <param name="body">The body of the email.</param>
    /// <param name="contentType">The content type of the email.</param>
    public EmailTemplate(string? fromAddress, string subject, string body, EmailContentType contentType)
    {
        Body = body;
        Subject = subject;
        ContentType = contentType;
        FromAddress = fromAddress ?? string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailTemplate"/> class.
    /// </summary>
    internal EmailTemplate()
    {
    }
}
