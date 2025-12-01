using System.Diagnostics;
using Microsoft.Extensions.Primitives;
using OpenTelemetry;

namespace Altinn.Notifications.Sms.Telemetry
{
    /// <summary>
    /// Filter for requests (and child dependencies) that should not be logged.
    /// </summary>
    public class RequestFilterProcessor : BaseProcessor<Activity>
    {
        private const string RequestKind = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestFilterProcessor"/> class.
        /// </summary>
        public RequestFilterProcessor(IHttpContextAccessor httpContextAccessor) : base()
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Determine whether to skip a request
        /// </summary>
        public override void OnStart(Activity activity)
        {
            bool skip = false;
            if (activity.OperationName == RequestKind)
            {
                skip = ExcludeRequest(_httpContextAccessor?.HttpContext?.Request?.Path.Value);
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
        /// <param name="activity">xx</param>
        public override void OnEnd(Activity activity)
        {
            if (activity.OperationName == RequestKind && _httpContextAccessor.HttpContext is not null && 
                _httpContextAccessor.HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues ipAddress))
            {
                activity.SetTag("ipAddress", ipAddress.FirstOrDefault());
            }
        }

        private static bool ExcludeRequest(string? localpath)
        {
            if (string.IsNullOrEmpty(localpath))
            {
                return false;
            }

            return localpath switch
            {
                var path when path.TrimEnd('/').EndsWith("/health", StringComparison.OrdinalIgnoreCase) => true,
                _ => false
            };
        }
    }
}
