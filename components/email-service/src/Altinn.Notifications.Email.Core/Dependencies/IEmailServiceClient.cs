using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core.Dependencies;

/// <summary>
/// Describes the public interface of a client able to send email requests to some mailing service.
/// </summary>
public interface IEmailServiceClient
{
    /// <summary>
    /// Sends a standard email to Azure Communication Services.
    /// </summary>
    /// <param name="email">The email to send.</param>
    /// <returns>The ACS operation ID on success, or an <see cref="EmailClientErrorResponse"/> on failure.</returns>
    Task<Result<string, EmailClientErrorResponse>> SendEmail(Sending.Email email);

    /// <summary>
    /// Downloads the attachments in <paramref name="email"/> via SAS URL, encodes them as base64,
    /// and submits the composed email to Azure Communication Services.
    /// </summary>
    /// <param name="email">The composed email with SAS-referenced attachments.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ComposedEmailSendResult"/> with the ACS operation ID and total encoded attachment size on success,
    /// or an <see cref="EmailClientErrorResponse"/> on failure.
    /// </returns>
    /// <exception cref="Exceptions.InvalidSasUrlException">Thrown when a SAS URL returns a permanent 4xx error. The caller should not retry.</exception>
    /// <exception cref="Exceptions.AttachmentDownloadException">Thrown when a transient network or HTTP error occurs while downloading an attachment. The caller should retry.</exception>
    Task<Result<ComposedEmailSendResult, EmailClientErrorResponse>> SendComposedEmail(Sending.ComposedEmail email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current delivery status of a previously submitted email send operation.
    /// </summary>
    /// <param name="operationId">The ACS operation ID.</param>
    /// <returns>The current send result for the operation.</returns>
    Task<EmailSendResult> GetOperationUpdate(string operationId);
}
