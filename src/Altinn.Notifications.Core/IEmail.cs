

namespace Altinn.Notifications.Core
{
    /// <summary>
    /// Interface for the email client
    /// </summary>
    public interface IEmail
    {
        /// <summary>
        /// Send an email asynchronously
        /// </summary>
        /// <param name="address">The email address to send to</param>
        /// <param name="subject">The subject of the email</param>
        /// <param name="body">The body of the email</param>
        /// <returns>True when email sendt successfully, false othervise</returns>
        Task<bool> SendEmailAsync(string address, string subject, string body, CancellationToken cancellationToken = default);
    }
}