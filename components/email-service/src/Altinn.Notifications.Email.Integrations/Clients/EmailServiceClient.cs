using Altinn.Notifications.Email.Core;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Clients;

/// <summary>
/// Represents an implementation of <see cref="IEmailServiceClient"/> that will use Azure Communication
/// Services to produce an email.
/// </summary>
public class EmailServiceClient : IEmailServiceClient
{
    private readonly ILogger<EmailServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailServiceClient"/> class.
    /// </summary>
    /// <param name="logger">A logger the class can use for logging.</param>
    public EmailServiceClient(ILogger<EmailServiceClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Send an email
    /// </summary>
    /// <param name="email">The email</param>
    /// <returns>A Task representing the asyncrhonous operation.</returns>
    /// <exception cref="NotImplementedException">Implementation pending</exception>
    public Task SendEmail(Core.Models.Email email)
    {
        _logger.LogError("Send email has not been implemented!");

        return Task.CompletedTask;
    }
}
