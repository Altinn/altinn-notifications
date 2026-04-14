using System.Diagnostics;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Azure.Messaging.ServiceBus;

using JasperFx;
using JasperFx.CodeGeneration;

using Npgsql;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Integrations.Wolverine.Policies;

/// <summary>
/// Wolverine handler policy that configures error handling for the <see cref="SmsDeliveryReportCommand"/> handler chain.
/// </summary>
/// <param name="settings">Wolverine settings used to retrieve retry and cooldown delay configuration.</param>
internal sealed class SmsDeliveryReportHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    private const string _retryExceededReason = "RETRY_THRESHOLD_EXCEEDED";
    private const string _notificationExpiredReason = "NOTIFICATION_EXPIRED";

    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(SmsDeliveryReportCommand))
            ?? throw new UnreachableException($"No handler chain found for {nameof(SmsDeliveryReportCommand)}. Ensure the handler is registered before adding this policy.");

        var policy = settings.SmsDeliveryReportQueuePolicy;

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
            .Then.SaveDeadDeliveryReport(_retryExceededReason, DeliveryReportChannel.LinkMobility);

        chain
            .OnException<NotificationExpiredException>()
            .SaveDeadDeliveryReport(_notificationExpiredReason, DeliveryReportChannel.LinkMobility);
    }
}
