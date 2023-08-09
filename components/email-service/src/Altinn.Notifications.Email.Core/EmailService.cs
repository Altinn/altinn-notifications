namespace Altinn.Notifications.Email.Core;

/// <summary>
/// Dummy class
/// </summary>
public class EmailService : IEmailService
{
    private readonly IEmailServiceClient _emailServiceClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class.
    /// </summary>
    /// <param name="emailServiceClient">A client that can perform actual mail sending.</param>
    public EmailService(IEmailServiceClient emailServiceClient)
    {
        _emailServiceClient = emailServiceClient;
    }

    /// <inheritdoc/>
    public async Task SendEmail(Models.Email email)
    {
        await _emailServiceClient.SendEmail(email);
    }
}
