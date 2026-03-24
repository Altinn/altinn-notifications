using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Status;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Sms.Integrations.Publishers;

/// <summary>
/// Publishes SMS delivery report results to the Azure Service Bus queue via Wolverine.
/// Registered in place of <see cref="KafkaSmsDeliveryReportPublisher"/> when
/// <c>EnableSmsDeliveryReportPublisher</c> is <c>true</c>.
/// </summary>
public class AsbSmsDeliveryReportPublisher(IServiceProvider serviceProvider) : ISmsDeliveryReportPublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <inheritdoc/>
    public async Task PublishAsync(SendOperationResult result)
    {
        var command = new SmsDeliveryReportCommand
        {
            NotificationId = result.NotificationId,
            GatewayReference = result.GatewayReference,
            SendResult = result.SendResult?.ToString() ?? string.Empty
        };

        using var scope = _serviceProvider.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command);
    }
}
