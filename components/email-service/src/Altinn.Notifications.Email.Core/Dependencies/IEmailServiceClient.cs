using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core.Dependencies;

/// <summary>
/// Describes the public interface of a client able to send email requests to some mailing service.
/// </summary>
public interface IEmailServiceClient
{
    /// <summary>
    /// Method for requesting the sending of an email.
    /// </summary>
    /// <param name="email">The email text</param>
    /// <returns>An operation id for tracing the success of the task or EmailClientErrorResponse if fail</returns>
    Task<Result<string, EmailClientErrorResponse>> SendEmail(Sending.Email email);

    /// <summary>
    /// Method for retrieving updated send status of an email.
    /// </summary>
    /// <param name="operationId">The operation id</param>
    /// <returns>An email send result based that correlates to the operation status</returns>
    Task<EmailSendResult> GetOperationUpdate(string operationId);
}
