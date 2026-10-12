using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="ISendSmsPublisher"/> that publishes
/// SMS notifications to an Azure Service Bus queue via <see cref="IMessageBusPublisher"/>.
/// </summary>
/// <param name="messageBusPublisher">The message bus publisher used to dispatch SMS commands.</param>
public class SendSmsCommandPublisher(IMessageBusPublisher messageBusPublisher) : ISendSmsPublisher
{
    private readonly IMessageBusPublisher _messageBusPublisher = messageBusPublisher;

    /// <inheritdoc/>
    public Task PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
            return _messageBusPublisher.PublishCommandAsync(CreateCommand(sms), cancellationToken);
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
