using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Integrations.Configuration;

using Azure.Messaging.ServiceBus;

using JasperFx;
using JasperFx.CodeGeneration;

using Npgsql;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Integrations.Wolverine.Policies;

/// <summary>
/// Wolverine handler policy that configures error handling for the <see cref="EmailDeliveryReportCommand"/> handler chain.
/// </summary>
/// <param name="settings">Wolverine settings used to retrieve retry and cooldown delay configuration.</param>
internal sealed class EmailDeliveryReportHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    private const string _retryExceededReason = "RETRY_THRESHOLD_EXCEEDED";
    private const string _notificationExpiredReason = "NOTIFICATION_EXPIRED";

    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(EmailDeliveryReportCommand));
        if (chain is null)
        {
            return;
        }

        var policy = settings.EmailDeliveryReportQueuePolicy;

        chain
            .OnException<InvalidOperationException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .Or<ServiceBusException>()
            .Or<NpgsqlException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();

        chain
            .OnException<NotificationNotFoundException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.SaveDeadDeliveryReport(_retryExceededReason, DeliveryReportChannel.AzureCommunicationServices);

        chain
            .OnException<NotificationExpiredException>()
            .SaveDeadDeliveryReport(_notificationExpiredReason, DeliveryReportChannel.AzureCommunicationServices);
    }
}
