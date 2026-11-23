using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using OpenTelemetry;

namespace Altinn.Notifications.Shared.Telemetry;

/// <summary>
/// Filter for requests (and child dependencies) that should not be logged.
/// </summary>
public class RequestFilterProcessor : BaseProcessor<Activity>
{
    /// <summary>The operation name of inbound HTTP request activities.</summary>
    protected const string RequestKind = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
    private static readonly string[] _excludedPathSuffixes = ["/health", "/health/live", "/health/ready", "/health/startup"];

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestFilterProcessor"/> class.
    /// </summary>
    public RequestFilterProcessor(IHttpContextAccessor? httpContextAccessor = null) : base()
    {
        HttpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the HTTP context accessor.
    /// </summary>
    protected IHttpContextAccessor? HttpContextAccessor { get; }

    /// <summary>
    /// Determine whether to skip a request
    /// </summary>
    public override void OnStart(Activity data)
    {
        bool skip = false;
        if (data.OperationName == RequestKind)
        {
            skip = ExcludeRequest(HttpContextAccessor?.HttpContext?.Request?.Path.Value);
        }
        else if (!(data.Parent?.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ?? true))
        {
            skip = true;
        }

        if (skip)
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }

    /// <summary>
    /// Captures the X-Forwarded-For IP address as an activity tag on inbound HTTP requests.
    /// </summary>
    /// <param name="data">The activity that has ended.</param>
    public override void OnEnd(Activity data)
    {
        if (data.OperationName == RequestKind
            && HttpContextAccessor?.HttpContext is not null
            && HttpContextAccessor.HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues ipAddress))
        {
            data.SetTag("ipAddress", ipAddress.FirstOrDefault());
        }
    }

    private static bool ExcludeRequest(string? localpath)
    {
        if (string.IsNullOrEmpty(localpath))
        {
            return false;
        }

        string path = localpath.TrimEnd('/');

        return _excludedPathSuffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
