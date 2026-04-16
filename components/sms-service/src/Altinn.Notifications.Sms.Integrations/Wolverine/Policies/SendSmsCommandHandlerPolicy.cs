using System.Diagnostics;

using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Sms.Integrations.Configuration;

using Azure.Messaging.ServiceBus;
using JasperFx;
using JasperFx.CodeGeneration;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Sms.Integrations.Wolverine.Policies;

/// <summary>
/// Wolverine handler policy for the SendSmsCommandHandler. This policy is responsible for applying any necessary
/// configurations or behaviors to the handler chains associated with sending SMS commands, like retry policies and error handling chains.
/// </summary>
/// <param name="wolverineSettings">Settings used to retrieve retry and cooldown delay configuration</param>
internal sealed class SendSmsCommandHandlerPolicy(WolverineSettings wolverineSettings) : IHandlerPolicy
{
    /// <summary>
    /// Configures retry and error handling policies for handler chains that process SendSmsCommand messages.
    /// </summary>
    /// <remarks>This method applies specific retry and error handling strategies to the handler chain
    /// responsible for SendSmsCommand messages. It sets up retries for certain exceptions, schedules additional
    /// retries, and moves failed messages to an error queue. Other handler chains are not affected.</remarks>
    /// <param name="chains">The collection of handler chains to configure. Each chain represents a message handling pipeline.</param>
    /// <param name="rules">The generation rules that influence how handlers and policies are applied.</param>
    /// <param name="container">The service container used to resolve dependencies required during configuration.</param>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var chain = chains.FirstOrDefault(c => c.MessageType == typeof(SendSmsCommand))
            ?? throw new UnreachableException($"No handler chain found for {nameof(SendSmsCommand)}. Ensure the handler is registered before adding this policy.");

        var policy = wolverineSettings.SendSmsQueuePolicy;

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
