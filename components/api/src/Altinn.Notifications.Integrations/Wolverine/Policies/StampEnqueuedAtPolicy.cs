using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;

using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Integrations.Wolverine.Policies;

/// <summary>
/// Global Wolverine handler policy that prepends <see cref="EnqueuedAtMiddleware"/>
/// to every handler chain, ensuring all queues stamp the original enqueue time
/// on the envelope before the handler runs.
/// </summary>
public sealed class StampEnqueuedAtPolicy : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(chain => !chain.Middleware
            .OfType<MethodCall>()
            .Any(m => m.HandlerType == typeof(EnqueuedAtMiddleware))))
        {
            chain.Middleware.Add(new MethodCall(typeof(EnqueuedAtMiddleware), nameof(EnqueuedAtMiddleware.Before)));
        }
    }
}
