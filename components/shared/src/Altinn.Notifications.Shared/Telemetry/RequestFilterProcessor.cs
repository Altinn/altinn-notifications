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
    private const string RequestKind = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestFilterProcessor"/> class.
    /// </summary>
    public RequestFilterProcessor(IHttpContextAccessor? httpContextAccessor = null) : base()
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Determine whether to skip a request
    /// </summary>
    public override void OnStart(Activity data)
    {
        bool skip = false;
        if (data.OperationName == RequestKind)
        {
            skip = ExcludeRequest(_httpContextAccessor?.HttpContext?.Request?.Path.Value);
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
            && _httpContextAccessor?.HttpContext is not null
            && _httpContextAccessor.HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues ipAddress))
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

        return localpath.TrimEnd('/').EndsWith("/health", StringComparison.OrdinalIgnoreCase);
    }
}
