using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Holds the email configuration settings for a notification order.
/// </summary>
public record EmailSendingOptions
{
    /// <summary>
    /// The optional sender address shown to the recipient; uses the system default when not set.
    /// </summary>
    public string? SenderEmailAddress { get; init; }

    /// <summary>
    /// The subject line displayed as the email headline in the recipient's inbox.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// The main body content of the email.
    /// </summary>
    /// <remarks>
    /// May include plain text or HTML markup depending on the <see cref="ContentType"/> setting.
    /// </remarks>
    public required string Body { get; init; }

    /// <summary>
    /// The file attachments to include in the email.
    /// </summary>
    /// <remarks>
    /// Each attachment is referenced by SAS URL and downloaded by the email service at send time.
    /// <c>null</c> for standard email orders; populated only for <see cref="Enums.OrderType.NotificationWithAttachments"/> orders.
    /// </remarks>
    public List<EmailAttachment>? Attachments { get; init; }

    /// <summary>
    /// The content type of the email body.
    /// </summary>
    /// <remarks>
    /// Determines how email clients render the body content. Defaults to <see cref="EmailContentType.Plain"/>.
    /// </remarks>
    public EmailContentType ContentType { get; init; } = EmailContentType.Plain;

    /// <summary>
    /// The policy that controls when the email may be delivered.
    /// </summary>
    public SendingTimePolicy SendingTimePolicy { get; init; } = SendingTimePolicy.Anytime;
}
