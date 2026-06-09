using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Integrations.Publishers;

/// <summary>
/// Publishes SMS delivery report results to the Azure Service Bus queue via Wolverine.
/// </summary>
public class SmsDeliveryReportPublisher(IServiceProvider serviceProvider) : WolverinePublisher(serviceProvider), ISmsDeliveryReportPublisher
{
    /// <inheritdoc/>
    public async Task PublishAsync(SendOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.SendResult is null)
        {
            throw new InvalidOperationException($"Cannot publish SMS delivery report for notification {result.NotificationId}: SendResult must not be null.");
        }

        var command = new SmsDeliveryReportCommand
        {
            NotificationId = result.NotificationId,
            GatewayReference = result.GatewayReference,
            SendResult = result.SendResult.Value.ToString(),
            DeliveryReport = result.DeliveryReport
        };

        await PublishCommandAsync(command);
    }
}
