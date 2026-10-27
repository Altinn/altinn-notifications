namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Extends <see cref="Email"/> with file attachments for composed email orders.
/// Attachments are resolved at send time by downloading the referenced blobs via SAS URL.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ComposedEmail"/> class.
/// </remarks>
/// <param name="notificationId">The notification identifier.</param>
/// <param name="subject">The email subject.</param>
/// <param name="body">The email body.</param>
/// <param name="fromAddress">The sender address.</param>
/// <param name="toAddress">The recipient address.</param>
/// <param name="contentType">The email content type.</param>
/// <param name="attachments">The list of SAS-referenced attachments.</param>
public class ComposedEmail(
    Guid notificationId,
    string subject,
    string body,
    string fromAddress,
    string toAddress,
    EmailContentType contentType,
    IReadOnlyList<SasFileAttachmentReference> attachments) : Email(notificationId, subject, body, fromAddress, toAddress, contentType)
{
    /// <summary>
    /// The file attachments to be downloaded and encoded before submission to ACS.
    /// </summary>
    public IReadOnlyList<SasFileAttachmentReference> Attachments { get; } = attachments;
}
