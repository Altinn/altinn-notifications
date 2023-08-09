namespace Altinn.Notifications.Email.Core;

/// <summary>
/// Describes the public interface of a client able to send email requests to some mailing service.
/// </summary>
public interface IEmailServiceClient
{
    /// <summary>
    /// Method for requesting the sending of an email.
    /// </summary>
    /// <param name="email">The email text</param>
    /// <returns>A task</returns>
    Task SendEmail(Models.Email email);
}
