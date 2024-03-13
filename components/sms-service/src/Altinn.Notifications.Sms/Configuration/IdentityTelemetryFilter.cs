using System.Diagnostics.CodeAnalysis;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Primitives;

namespace Altinn.Notifications.Sms.Configuration
{
    /// <summary>
    /// Filter to enrich request telemetry with identity information
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class IdentityTelemetryFilter : ITelemetryProcessor
    {
        private ITelemetryProcessor Next { get; set; }

        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityTelemetryFilter"/> class.
        /// </summary>
        public IdentityTelemetryFilter(ITelemetryProcessor next, IHttpContextAccessor httpContextAccessor)
        {
            Next = next;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public void Process(ITelemetry item)
        {
            RequestTelemetry? request = item as RequestTelemetry;

            if (request != null && request.Url.ToString().Contains("storage/api/"))
            {
                HttpContext? ctx = _httpContextAccessor.HttpContext;

                #pragma warning disable SA1305 // Field names should not use Hungarian notation
                if (ctx != null && ctx.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues ipAddress))
                {
                    request.Properties.Add("ipAddress", ipAddress.FirstOrDefault());
                }
                #pragma warning restore SA1305 // Field names should not use Hungarian notation
            }

            Next.Process(item);
        }
    }
}
