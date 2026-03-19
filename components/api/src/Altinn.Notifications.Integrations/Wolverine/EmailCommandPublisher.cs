using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailCommandPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class EmailCommandPublisher : IEmailCommandPublisher
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<EmailCommandPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailCommandPublisher"/> class.
    /// </summary>
    public EmailCommandPublisher(ILogger<EmailCommandPublisher> logger, IMessageBus messageBus)
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

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "EmailCommandPublisher failed to publish email notification {NotificationId} to ASB queue. ToAddress: {ToAddress}",
                email.NotificationId,
                email.ToAddress);

            return email.NotificationId;
        }
    }
}
