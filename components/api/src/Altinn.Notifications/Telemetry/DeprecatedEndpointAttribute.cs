using System.Diagnostics.Metrics;

using Altinn.Notifications.Extensions;

using Microsoft.AspNetCore.Mvc.Filters;

namespace Altinn.Notifications.Telemetry;

/// <summary>
/// Marks an endpoint as deprecated.
/// Every call emits one structured warning and increments a counter metric, so that actual usage can be determined before the endpoint is removed.
/// </summary>
/// <remarks>
/// The counter is not subject to trace sampling and is the authoritative signal for usage.
/// Example query: <c>customMetrics | where name == "altinn.notifications.deprecated.endpoint.calls" | summarize calls = sum(valueSum) by tostring(customDimensions.endpoint), tostring(customDimensions.creator)</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DeprecatedEndpointAttribute : ActionFilterAttribute
{
    private static readonly string _meterName = "Altinn.Notifications.DeprecatedEndpoints";

    private static readonly string _counterName = "altinn.notifications.deprecated.endpoint.calls";

    private static readonly Meter _meter = new(MeterName);

    private static readonly Counter<long> _counter = _meter.CreateCounter<long>(CounterName, description: "Number of calls to deprecated endpoints.");

    /// <inheritdoc/>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        HttpContext httpContext = context.HttpContext;

        string method = httpContext.Request.Method;
        string creator = httpContext.GetOrg() ?? "unknown";
        string endpoint = context.ActionDescriptor.DisplayName ?? httpContext.Request.Path.Value ?? "unknown";
        string route = context.ActionDescriptor.AttributeRouteInfo?.Template ?? httpContext.Request.Path.Value ?? "unknown";

        ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<DeprecatedEndpointAttribute>();

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

        base.OnActionExecuting(context);
    }

    /// <summary>
    /// The name of the meter. Must be registered via <c>AddMeter</c> in <c>Program.cs</c>.
    /// </summary>
    public static string MeterName => _meterName;

    /// <summary>
    /// The name of the counter metric.
    /// </summary>
    public static string CounterName => _counterName;
}
