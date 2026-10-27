using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Integrations.Publishers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="ISmsSendResultDispatcher"/> that dispatches
/// an <see cref="SmsSendResultCommand"/> via Wolverine to publish terminal SMS send operation results.
/// </summary>
public class SmsSendResultPublisher(IServiceProvider serviceProvider) : WolverinePublisher(serviceProvider), ISmsSendResultDispatcher
{
    /// <inheritdoc/>
    public async Task DispatchAsync(SendOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.SendResult is null)
        {
            throw new ArgumentException("SendResult must be set before dispatching.", nameof(result));
        }

        if (result.NotificationId is null)
        {
            throw new ArgumentException("NotificationId must be set before dispatching.", nameof(result));
        }

        if (result.NotificationId == Guid.Empty)
        {
            throw new ArgumentException("NotificationId must not be empty.", nameof(result));
        }

        var command = new SmsSendResultCommand
        {
            NotificationId = result.NotificationId.Value,

            // SendResult.ToString() is the wire format; SmsNotificationResultType on the API side
            // must have matching member names — any divergence will be treated as an unrecognized result.
            SendResult = result.SendResult.Value.ToString(),
            GatewayReference = string.IsNullOrWhiteSpace(result.GatewayReference) ? null : result.GatewayReference
        };

        await PublishCommandAsync(command);
    }
}
