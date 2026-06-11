namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Record representing an email attachment.
/// </summary>
/// <param name="Name">The file name of the attachment.</param>
/// <param name="ContentType">The MIME type of the attachment.</param>
/// <param name="Base64Content">The base64-encoded content of the attachment.</param>
public record EmailAttachment(string Name, string ContentType, string Base64Content);
