using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="ISendSmsPublisher"/> that publishes
/// SMS notifications to an Azure Service Bus queue via <see cref="IMessageBusPublisher"/>.
/// </summary>
/// <param name="logger">The logger used to record operational events and errors during SMS publishing.</param>
/// <param name="messageBusPublisher">The message bus publisher used to dispatch SMS commands.</param>
public class SendSmsCommandPublisher(ILogger<SendSmsCommandPublisher> logger, IMessageBusPublisher messageBusPublisher) : ISendSmsPublisher
{
    private readonly ILogger<SendSmsCommandPublisher> _logger = logger;
    private readonly IMessageBusPublisher _messageBusPublisher = messageBusPublisher;

    /// <inheritdoc/>
    public async Task<Sms?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _messageBusPublisher.PublishCommandAsync(CreateCommand(sms), cancellationToken);

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
                "SendSmsCommandPublisher failed to publish SMS notification {NotificationId} to ASB queue.",
                sms.NotificationId);

            return sms;
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken)
    {
        return _messageBusPublisher.PublishBatchAsync(
            smsList,
            commandFactory: CreateCommand,
            onError: (sms, exception) =>
            {
                if (exception is OperationCanceledException)
                {
                    _logger.LogInformation(
                        exception,
                        "SendSmsCommandPublisher cancelled before publishing SMS notification {NotificationId}; reporting as unpublished.",
                        sms.NotificationId);
                    return;
                }

                _logger.LogError(exception, "SendSmsCommandPublisher failed to publish SMS notification {NotificationId} to ASB queue.", sms.NotificationId);
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a <see cref="SendSmsCommand"/> from the provided <see cref="Sms"/> instance.
    /// </summary>
    /// <param name="sms">The SMS message to convert into a command.</param>
    /// <returns>A <see cref="SendSmsCommand"/> representing the SMS message.</returns>
    private static SendSmsCommand CreateCommand(Sms sms)
    {
        return new SendSmsCommand
        {
            MobileNumber = sms.Recipient,
            Body = sms.Message,
            SenderNumber = sms.Sender,
            NotificationId = sms.NotificationId
        };
    }
}
