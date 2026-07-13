namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Describes the required public method of the email service.
/// </summary>
public interface ISendingService
{
    /// <summary>
    /// Sends a standard email via Azure Communication Services.
    /// </summary>
    /// <param name="email">The email to send.</param>
    Task SendAsync(Email email);

    /// <summary>
    /// Downloads attachments via SAS URL and sends the composed email via ACS.
    /// </summary>
    /// <param name="email">The composed email with SAS-referenced attachments.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <exception cref="Exceptions.AttachmentDownloadException">Propagated when a transient network or HTTP error occurs downloading an attachment. Wolverine will retry.</exception>
    Task SendComposedAsync(ComposedEmail email, CancellationToken cancellationToken = default);
}
