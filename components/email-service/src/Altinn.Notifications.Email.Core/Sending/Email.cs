using System.Collections.Immutable;

namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Record representing an email.
/// </summary>
/// <param name="NotificationId">The notification ID associated with the email.</param>
/// <param name="Attachments">The attachments to include with the email.</param>
/// <param name="Body">The body of the email.</param>
/// <param name="ContentType">The content type of the email body.</param>
/// <param name="FromAddress">The sender address of the email.</param>
/// <param name="Subject">The subject of the email.</param>
/// <param name="ToAddress">The recipient address of the email.</param>
public record Email(Guid NotificationId, ImmutableList<EmailAttachment> Attachments, string Body, EmailContentType ContentType, string FromAddress, string Subject, string ToAddress);
