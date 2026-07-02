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
    /// Downloads attachments via SAS URL, encodes them as base64, and sends the composed email via ACS.
    /// </summary>
    /// <param name="email">The composed email with SAS-referenced attachments.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    Task SendComposedAsync(ComposedEmail email, CancellationToken cancellationToken = default);
}
