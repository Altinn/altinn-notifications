namespace Altinn.Notifications.Email.Core;

/// <summary>
/// Describes the required public method of the email service.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send an email
    /// </summary>
    /// <param name="email">The details for an email to be sent.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendEmail(Models.Email email);
}
