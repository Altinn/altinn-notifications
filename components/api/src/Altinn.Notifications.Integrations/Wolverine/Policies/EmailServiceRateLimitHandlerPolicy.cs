using System.Diagnostics;

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
/// Wolverine handler policy that configures error handling for the <see cref="EmailServiceRateLimitCommand"/> handler chain.
/// </summary>
/// <param name="settings">Wolverine settings used to retrieve retry and cooldown delay configuration.</param>
internal sealed class EmailServiceRateLimitHandlerPolicy(WolverineSettings settings) : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(EmailServiceRateLimitCommand))
            ?? throw new UnreachableException($"No handler chain found for {nameof(EmailServiceRateLimitCommand)}. Ensure the handler is registered before adding this policy.");

        var policy = settings.EmailServiceRateLimitQueuePolicy;

        chain
            .OnException<InvalidOperationException>()
            .Or<NpgsqlException>()
            .Or<TimeoutException>()
            .Or<ServiceBusException>()
            .Or<TaskCanceledException>()
            .RetryWithCooldown(policy.GetCooldownDelays())
            .Then.ScheduleRetry(policy.GetScheduleDelays())
            .Then.MoveToErrorQueue();
    }
}
