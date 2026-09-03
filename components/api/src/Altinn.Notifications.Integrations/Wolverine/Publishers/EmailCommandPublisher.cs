using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailCommandPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBusPublisher"/>.
/// </summary>
public class EmailCommandPublisher(ILogger<EmailCommandPublisher> logger, IMessageBusPublisher messageBusPublisher, IOptions<WolverineSettings> options) : IEmailCommandPublisher
{
    private readonly ILogger<EmailCommandPublisher> _logger = logger;
    private readonly IMessageBusPublisher _messageBusPublisher = messageBusPublisher;
    private readonly int _publishConcurrency = options.Value.EmailPublishConcurrency <= 0 ? 10 : options.Value.EmailPublishConcurrency;

    /// <inheritdoc/>
    public async Task<Email?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _messageBusPublisher.PublishCommandAsync(CreateCommand(email), cancellationToken);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailCommandPublisher failed to publish email notification {NotificationId} to ASB queue.", email.NotificationId);
            return email;
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Email>> PublishAsync(IReadOnlyList<Email> emails, CancellationToken cancellationToken)
    {
        return _messageBusPublisher.PublishBatchAsync(
            emails,
            commandFactory: CreateCommand,
            onError: (email, exception) =>
            {
                if (exception is OperationCanceledException)
                {
                    _logger.LogInformation(
                        exception,
                        "EmailCommandPublisher cancelled before publishing email notification {NotificationId}; reporting as unpublished.",
                        email.NotificationId);
                    return;
                }

                _logger.LogError(exception, "EmailCommandPublisher failed to publish email notification {NotificationId} to ASB queue.", email.NotificationId);
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a <see cref="SendEmailCommand"/> instance from the given <see cref="Email"/>.
    /// </summary>
    /// <param name="email">The email message to convert into a command.</param>
    /// <returns>A <see cref="SendEmailCommand"/> representing the email message.</returns>
    private static SendEmailCommand CreateCommand(Email email)
    {
        return new SendEmailCommand
        {
            Body = email.Body,
            Subject = email.Subject,
            ToAddress = email.ToAddress,
            FromAddress = email.FromAddress,
            NotificationId = email.NotificationId,
            ContentType = email.ContentType.ToString()
        };
    }
}
