using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Publishes SMS messages asynchronously using Wolverine message bus.
/// </summary>
/// <remarks>
/// This class implements the <see cref="ISendSmsCommandPublisher"/> interface to enable asynchronous publication of SMS
/// commands to Azure Service Bus via Wolverine. Ensure that the provided <see cref="Sms"/> object is properly configured before calling <see cref="PublishAsync"/>.
/// </remarks>
/// <param name="logger">The logger used to record operational events and errors during SMS publishing.</param>
/// <param name="serviceProvider">The service provider used to resolve dependencies required for publishing SMS messages.</param>
public class SendSmsCommandPublisher(ILogger<SendSmsCommandPublisher> logger, IServiceProvider serviceProvider) : ISendSmsCommandPublisher
{
    /// <summary>
    /// Publishes an SMS message asynchronously to the message bus.
    /// </summary>
    /// <remarks>
    /// This method converts the <see cref="Sms"/> object into a <see cref="SendSmsCommand"/> and publishes it
    /// via Wolverine's message bus. If publishing fails, the exception is logged and the notification ID is returned
    /// to signal that the message should be retried.
    /// </remarks>
    /// <param name="sms">The SMS message to be published. Must contain the message content and recipient information.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the publish operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. Returns <c>null</c> if the SMS was successfully published to the message bus,
    /// or the <see cref="Sms.NotificationId"/> if the publish operation failed (indicating the message should be retried).
    /// </returns>
    public async Task<Guid?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        var sendSmsCommand = new SendSmsCommand();

        try
        {
            sendSmsCommand.MobileNumber = sms.Recipient;
            sendSmsCommand.Body = sms.Message;
            sendSmsCommand.SenderNumber = sms.Sender;
            sendSmsCommand.NotificationId = sms.NotificationId;

            await using var scope = serviceProvider.CreateAsyncScope();
            var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            
            await messageBus.SendAsync(sendSmsCommand);
            return null; // Success - no retry needed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish SMS command for NotificationId: {NotificationId}", sms.NotificationId);
            return sendSmsCommand.NotificationId; // Failure - return ID to signal retry needed
        }
    }
}
