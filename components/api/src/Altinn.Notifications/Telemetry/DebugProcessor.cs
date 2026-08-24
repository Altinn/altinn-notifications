using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Altinn.AccessManagement.Core.Models;
using AltinnCore.Authentication.Constants;
using Microsoft.Extensions.Primitives;
using OpenTelemetry;

namespace Altinn.Notifications.Telemetry
{
    /// <summary>
    /// Filter for requests (and child dependencies) that should not be logged.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DebugProcessor"/> class.
    /// </remarks>
    public class DebugProcessor() : BaseProcessor<Activity>()
    {
        /// <summary>
        /// Determine whether to skip a request
        /// </summary>
        public override void OnStart(Activity activity)
        {
            bool skip = false;
            if (activity.OperationName == RequestKind)
            {
                var path = _httpContextAccessor.HttpContext?.Request.Path.Value;
                if (path != null)
                {
                    skip = ExcludeRequest(path);
                }
            }
            else if (!(activity.Parent?.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ?? true))
            {
                skip = true;
            }

            if (skip)
            {
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }

        /// <summary>
        /// No action on end
        /// </summary>
        /// <param name="data">xx</param>
        public override void OnEnd(Activity data)
        {
            Console.WriteLine($"SPAN ENDED: {data.DisplayName} TraceId={data.TraceId}");
        }
    }
}
