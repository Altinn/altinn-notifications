using System.Diagnostics;

using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine;
using Altinn.Notifications.Integrations.Wolverine.Commands;

using Azure.Messaging.ServiceBus;

using JasperFx;
using JasperFx.CodeGeneration;

using Npgsql;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Integrations.Wolverine.Policies;

/// <summary>
/// Wolverine handler policy that configures error handling for the <see cref="ProcessPastDueOrderCommand"/> handler chain.
/// </summary>
/// <param name="settings">Wolverine settings used to retrieve retry and cooldown delay configuration.</param>
internal sealed class ProcessPastDueOrderHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(ProcessPastDueOrderCommand))
            ?? throw new UnreachableException($"No handler chain found for {nameof(ProcessPastDueOrderCommand)}. Ensure the handler is registered before adding this policy.");

        var policy = settings.PastDueOrdersQueuePolicy;

        chain
            .OnException<TimeoutException>()
            .Or<ServiceBusException>()
            .Or<NpgsqlException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();

        chain
            .OnException<SendConditionInconclusiveException>()
            .Or<PlatformDependencyException>()
            .ScheduleRetry(TimeSpan.FromMinutes(1))
            .Then.MoveToErrorQueue();
    }
}
