using System.Diagnostics;

using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Azure.Messaging.ServiceBus;

using JasperFx;
using JasperFx.CodeGeneration;

using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Email.Integrations.Wolverine.Policies;

/// <summary>
/// Wolverine handler policy that configures error handling for the <see cref="SendEmailCommand"/> handler chain.
/// </summary>
/// <param name="settings">Wolverine settings used to retrieve retry and cooldown delay configuration.</param>
internal sealed class SendEmailCommandHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(SendEmailCommand))
            ?? throw new UnreachableException($"No handler chain found for {nameof(SendEmailCommand)}. Ensure the handler is registered before adding this policy.");

        var policy = settings.EmailSendQueuePolicy;

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
