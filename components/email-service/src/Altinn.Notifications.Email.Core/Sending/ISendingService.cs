namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Describes the required public method of the email service.
/// </summary>
public interface ISendingService
{
    /// <summary>
    /// Send an email
    /// </summary>
    /// <param name="email">The details for an email to be sent.</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync(Email email);
}
