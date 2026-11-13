using System.Diagnostics.Metrics;

using Altinn.Notifications.Extensions;

using Microsoft.AspNetCore.Mvc.Filters;

namespace Altinn.Notifications.Telemetry;

/// <summary>
/// Action filter that records calls to endpoints marked with <see cref="DeprecatedEndpointAttribute"/>.
/// </summary>
/// <param name="logger">The logger used to emit the deprecation warning.</param>
public sealed class DeprecatedEndpointFilter(ILogger<DeprecatedEndpointFilter> logger) : IActionFilter
{
    /// <summary>
    /// The name of the meter.
    /// </summary>
    public static string MeterName { get; } = "Altinn.Notifications.DeprecatedEndpoints";

    /// <summary>
    /// The name of the counter metric.
    /// </summary>
    public static string CounterName { get; } = "altinn.notifications.deprecated.endpoint.calls";

    private static readonly Meter _meter = new(MeterName);

    private static readonly Counter<long> _counter = _meter.CreateCounter<long>(CounterName, description: "Number of calls to deprecated endpoints.");

    /// <inheritdoc/>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        HttpContext httpContext = context.HttpContext;

        string method = httpContext.Request.Method;
        string creator = httpContext.GetOrg() ?? "unknown";
        string endpoint = context.ActionDescriptor.DisplayName ?? httpContext.Request.Path.Value ?? "unknown";
        string route = context.ActionDescriptor.AttributeRouteInfo?.Template ?? httpContext.Request.Path.Value ?? "unknown";

        logger.LogWarning(
            "DeprecatedEndpointCalled: {Endpoint} {Method} {Route} by {Creator}",
            endpoint,
            method,
            route,
            creator);

        _counter.Add(
            1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("route", route),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("creator", creator));
    }

    /// <inheritdoc/>
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
