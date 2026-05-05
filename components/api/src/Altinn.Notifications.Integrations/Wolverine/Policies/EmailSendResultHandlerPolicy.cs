using System.Diagnostics;

using Altinn.Notifications.Core.Enums;
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
/// Wolverine handler policy that configures error handling for the <see cref="EmailSendResultCommand"/> handler chain.
/// </summary>
/// <param name="settings">Wolverine settings used to retrieve retry and cooldown delay configuration.</param>
internal sealed class EmailSendResultHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    private const string _unrecognizedSendResultReason = "UNRECOGNIZED_SEND_RESULT";

    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(EmailSendResultCommand))
            ?? throw new UnreachableException($"No handler chain found for {nameof(EmailSendResultCommand)}. Ensure the handler is registered before adding this policy.");

        var policy = settings.EmailSendResultQueuePolicy;

        chain
            .OnException<InvalidOperationException>()
            .Or<NpgsqlException>()
            .Or<TimeoutException>()
            .Or<ServiceBusException>()
            .Or<TaskCanceledException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();

        chain
            .OnException<ArgumentException>()
            .SaveDeadDeliveryReport(_unrecognizedSendResultReason, DeliveryReportChannel.AzureCommunicationServices);
    }
}
