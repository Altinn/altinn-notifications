using Microsoft.AspNetCore.Mvc;

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
public sealed class DeprecatedEndpointAttribute : TypeFilterAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeprecatedEndpointAttribute"/> class.
    /// </summary>
    public DeprecatedEndpointAttribute()
        : base(typeof(DeprecatedEndpointFilter))
    {
    }

    /// <summary>
    /// The name of the meter. Must be registered via <c>AddMeter</c> in <c>Program.cs</c>.
    /// </summary>
    public static string MeterName => DeprecatedEndpointFilter.MeterName;

    /// <summary>
    /// The name of the counter metric.
    /// </summary>
    public static string CounterName => DeprecatedEndpointFilter.CounterName;
}
