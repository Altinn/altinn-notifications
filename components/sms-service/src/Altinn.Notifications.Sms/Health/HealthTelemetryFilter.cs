using System.Diagnostics.CodeAnalysis;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Altinn.Notifications.Sms.Health
{
    /// <summary>
    /// Filter to exclude health check request from Application Insights
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="HealthTelemetryFilter"/> class.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public class HealthTelemetryFilter(ITelemetryProcessor next) : ITelemetryProcessor
    {
        private ITelemetryProcessor Next { get; set; } = next;

        /// <inheritdoc/>
        public void Process(ITelemetry item)
        {
            if (ExcludeItemTelemetry(item))
            {
                return;
            }

            Next.Process(item);
        }

        private static bool ExcludeItemTelemetry(ITelemetry item)
        {
            return item is RequestTelemetry request && request.Url.ToString().EndsWith("/health");
        }
    }
}
