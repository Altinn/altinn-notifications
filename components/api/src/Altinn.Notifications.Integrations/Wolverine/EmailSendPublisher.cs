using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailSendPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class EmailSendPublisher : IEmailSendPublisher
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<EmailSendPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendPublisher"/> class.
    /// </summary>
    public EmailSendPublisher(ILogger<EmailSendPublisher> logger, IMessageBus messageBus)
    {
        _logger = logger;
        _messageBus = messageBus;
    }

    /// <inheritdoc/>
    public async Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        var sendEmailCommand = new SendEmailCommand
        {
            Body = email.Body,
            Subject = email.Subject,
            ToAddress = email.ToAddress,
            FromAddress = email.FromAddress,
            NotificationId = email.NotificationId,
            ContentType = email.ContentType.ToString()
        };

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _messageBus.SendAsync(sendEmailCommand);

            _logger.LogInformation("// EmailSendPublisher // PublishAsync // Successfully published email notification {NotificationId}.", email.NotificationId);

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "// EmailSendPublisher // PublishAsync // Failed to publish email notification {NotificationId}.", email.NotificationId);

            return email.NotificationId;
        }
    }
}
