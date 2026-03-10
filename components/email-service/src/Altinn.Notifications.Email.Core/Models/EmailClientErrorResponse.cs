using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core.Models;

/// <summary>
/// A class representing a email send response object
/// </summary>
public class EmailClientErrorResponse
{
    /// <summary>
    /// Result for the email send operation
    /// </summary>
    public EmailSendResult SendResult { get; set; }

    /// <summary>
    /// The delay in seconds before Azure Communication Services can receive new emails
    /// </summary>
    public int? IntermittentErrorDelay { get; set; }
}
