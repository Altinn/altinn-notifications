using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Files;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Extends <see cref="Email"/> with a list of file attachments for composed email orders.
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
/// <param name="attachments">The list of SAS-referenced file attachments.</param>
public class ComposedEmail(
    Guid notificationId,
    string subject,
    string body,
    string fromAddress,
    string toAddress,
    EmailContentType contentType,
    IReadOnlyList<SasFileReference> attachments) : Email(notificationId, subject, body, fromAddress, toAddress, contentType)
{
    /// <summary>
    /// The file attachments to be downloaded and encoded at send time.
    /// </summary>
    public IReadOnlyList<SasFileReference> Attachments { get; } = attachments;
}
