using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.NotificationTemplate;

/// <summary>
/// Template for an email notification
/// </summary>
public class EmailTemplate : INotificationTemplate
{
    /// <summary>
    /// Gets the from adress of emails created by the template    
    /// </summary>
    public string FromAddress { get; private set; }

    /// <summary>
    /// Gets the subject of emails created by the template    
    /// </summary>
    public string Subject { get; private set; }

    /// <summary>
    /// Gets the body of emails created by the template    
    /// </summary>
    public string Body { get; private set; }

    /// <summary>
    /// Gets the content type of emails created by the template
    /// </summary>
    public EmailContentType ContentType { get; private set; }

    /// <inheritdoc/>
    public NotificationTemplateType Type { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailTemplate"/> class.
    /// </summary>
    public EmailTemplate(string fromAddress, string subject, string body, EmailContentType contentType)
    {
        FromAddress = fromAddress;
        Subject = subject;
        Body = body;
        ContentType = contentType;
        Type = NotificationTemplateType.Email;
    }
}