using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="ISendSmsPublisher"/> that publishes
/// SMS notifications to an Azure Service Bus queue via <see cref="IMessageBusPublisher"/>.
/// </summary>
/// <param name="logger">The logger used to record operational events and errors during SMS publishing.</param>
/// <param name="messageBusPublisher">The message bus publisher used to dispatch SMS commands.</param>
/// <param name="options">Configuration options for Wolverine settings, including SMS publish concurrency.</param>
public class SendSmsCommandPublisher(ILogger<SendSmsCommandPublisher> logger, IMessageBusPublisher messageBusPublisher, IOptions<WolverineSettings> options) : ISendSmsPublisher
{
    private readonly ILogger<SendSmsCommandPublisher> _logger = logger;
    private readonly IMessageBusPublisher _messageBusPublisher = messageBusPublisher;
    private readonly int _publishConcurrency = options.Value.SmsPublishConcurrency <= 0 ? 10 : options.Value.SmsPublishConcurrency;

    /// <inheritdoc/>
    public async Task<Sms?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        var sendSmsCommand = new SendSmsCommand
        {
            MobileNumber = sms.Recipient,
            Body = sms.Message,
            SenderNumber = sms.Sender,
            NotificationId = sms.NotificationId
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _messageBusPublisher.PublishCommandAsync(sendSmsCommand, cancellationToken);

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
            commandFactory: sms => new SendSmsCommand
            {
                MobileNumber = sms.Recipient,
                Body = sms.Message,
                SenderNumber = sms.Sender,
                NotificationId = sms.NotificationId
            },
            concurrency: _publishConcurrency,
            onError: (sms, ex) =>
            {
                _logger.LogError(ex, "SendSmsCommandPublisher failed to publish SMS notification {NotificationId} to ASB queue.", sms.NotificationId);
            },
            cancellationToken: cancellationToken);
    }
}
