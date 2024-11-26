using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.NotificationTemplate;

/// <summary>
/// Template for an SMS notification.
/// </summary>
public class SmsTemplate : INotificationTemplate
{
    /// <summary>
    /// Gets the body of the SMS.
    /// </summary>
    public string Body { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the number from which the SMS is sent.
    /// </summary>
    public string SenderNumber { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the type of the notification template.
    /// </summary>
    /// <value>
    /// The type of the notification template, represented by the <see cref="NotificationTemplateType" /> enum.
    /// </value>
    public NotificationTemplateType Type { get; internal set; } = NotificationTemplateType.Sms;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsTemplate"/> class with the specified sender number and body.
    /// </summary>
    /// <param name="senderNumber">The number from which the SMS is sent. If null, an empty string is used.</param>
    /// <param name="body">The body of the SMS.</param>
    public SmsTemplate(string? senderNumber, string body)
    {
        Body = body;
        SenderNumber = senderNumber ?? string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsTemplate"/> class.
    /// </summary>
    internal SmsTemplate()
    {
    }
}
