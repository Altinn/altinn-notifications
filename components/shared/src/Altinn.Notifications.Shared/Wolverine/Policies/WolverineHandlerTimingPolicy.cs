using Altinn.Notifications.Shared.Wolverine.Middleware;

using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;

using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace Altinn.Notifications.Shared.Wolverine.Policies;

/// <summary>
/// Applies handler timing middleware to Wolverine message handler chains.
/// </summary>
public sealed class WolverineHandlerTimingPolicy : IHandlerPolicy
{
    /// <inheritdoc/>
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains)
        {
            chain.Middleware.Insert(0, new MethodCall(typeof(WolverineHandlerTimingMiddleware), nameof(WolverineHandlerTimingMiddleware.Before)));
            chain.Postprocessors.Add(new MethodCall(typeof(WolverineHandlerTimingMiddleware), nameof(WolverineHandlerTimingMiddleware.After)));
        }
    }
}
