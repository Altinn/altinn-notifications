using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Publishers;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="ISmsCommandPublisher"/> that publishes
/// SMS notifications to an Azure Service Bus queue via <see cref="IWolverinePublisher"/>.
/// </summary>
/// <param name="wolverinePublisher">The publisher used to dispatch SMS commands.</param>
public class SmsCommandPublisher(IWolverinePublisher wolverinePublisher) : ISmsCommandPublisher
{
    /// <inheritdoc/>
    public async Task PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        try
        {
            await wolverinePublisher.PublishCommandAsync(CreateCommand(sms), cancellationToken);
        }
        catch (ContentTooLargeException ex)
        {
            throw new NotificationSizeLimitExceededException($"SMS {sms.NotificationId} exceeds the message queue size limit.", ex);
        }
    }

    /// <summary>
    /// Creates a <see cref="SendSmsCommand"/> from an <see cref="Sms"/>.
    /// </summary>
    /// <param name="sms">The SMS to convert.</param>
    /// <returns>The command to publish to the message bus.</returns>
    private static SendSmsCommand CreateCommand(Sms sms)
    {
        return new SendSmsCommand
        {
            Body = sms.Message,
            MobileNumber = sms.Recipient,
            SenderNumber = sms.Sender,
            NotificationId = sms.NotificationId
        };
    }
}
