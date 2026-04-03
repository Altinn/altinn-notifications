using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Publishes SMS messages asynchronously using the configured command infrastructure.
/// </summary>
/// <remarks>This class implements the ISmsCommandPublisher interface to enable asynchronous publication of SMS
/// commands. Ensure that the provided Sms object is properly configured before calling PublishAsync.</remarks>
/// <param name="logger">The logger used to record operational events and errors during SMS publishing.</param>
/// <param name="serviceProvider">The service provider used to resolve dependencies required for publishing SMS messages.</param>
public class SendSmsCommandPublisher(ILogger<SendSmsCommandPublisher> logger, IServiceProvider serviceProvider) : ISendSmsCommandPublisher
{
    /// <summary>
    /// Publishes an SMS message asynchronously and returns the unique identifier of the published message.
    /// </summary>
    /// <remarks>This method may throw exceptions if the SMS message is invalid or if there are issues with
    /// the publishing process.</remarks>
    /// <param name="sms">The SMS message to be published. Must contain the message content and recipient information.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the publish operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the
    /// published SMS message, or null if the operation fails.</returns>
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
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish SMS command for NotificationId: {NotificationId}", sms.NotificationId);
            return sendSmsCommand.NotificationId;
        }
    }
}
