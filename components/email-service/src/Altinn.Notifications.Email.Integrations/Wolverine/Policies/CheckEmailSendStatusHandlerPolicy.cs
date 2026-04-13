using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Integrations.Configuration;

using Azure.Messaging.ServiceBus;

using JasperFx;
using JasperFx.CodeGeneration;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Policies;

/// <summary>
/// Wolverine handler policy that configures error handling for the <see cref="CheckEmailSendStatusCommand"/> handler chain.
/// </summary>
/// <param name="settings">Wolverine settings used to retrieve retry and cooldown delay configuration.</param>
internal sealed class CheckEmailSendStatusHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(CheckEmailSendStatusCommand));
        if (chain is null)
        {
            return;
        }

        var policy = settings.EmailStatusCheckQueuePolicy;

        chain
            .OnException<TimeoutException>()
            .Or<ServiceBusException>()
            .Or<TaskCanceledException>()
            .Or<InvalidOperationException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }
}
