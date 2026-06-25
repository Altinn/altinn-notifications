namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Holds the email configuration and file attachments for an email-with-attachments notification order.
/// </summary>
public record EmailWithAttachmentsSendingOptions : EmailSendingOptions
{
    /// <summary>
    /// One or more file attachments to include in the email.
    /// </summary>
    /// <remarks>
    /// Each attachment is referenced by SAS URL and fetched by the email service at send time.
    /// </remarks>
    public required List<EmailAttachment> Attachments { get; init; }
}
