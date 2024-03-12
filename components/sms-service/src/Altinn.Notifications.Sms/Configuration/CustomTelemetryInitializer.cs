using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Altinn.Notifications.Sms.Configuration;

/// <summary>
/// Set up custom telemetry for Application Insights
/// </summary>
public class CustomTelemetryInitializer : ITelemetryInitializer
{
    /// <summary>
    /// Custom TelemetryInitializer that sets some specific values for the component
    /// </summary>
    public void Initialize(ITelemetry telemetry)
    {
        if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
        {
            telemetry.Context.Cloud.RoleName = "platform-notifications-sms";
        }

        // Disable sampling for exceptions, requests, dependencies and cleanup
        if (telemetry is RequestTelemetry requestTelemetry)
        {
            ((ISupportSampling)telemetry).SamplingPercentage = 100;
            requestTelemetry.ProactiveSamplingDecision = SamplingDecision.SampledIn;
        }
        else if (telemetry is DependencyTelemetry dependencyTelemetry)
        {
            ((ISupportSampling)telemetry).SamplingPercentage = 100;
            dependencyTelemetry.ProactiveSamplingDecision = SamplingDecision.SampledIn;
        }
        else if (telemetry is ExceptionTelemetry exceptionTelemetry)
        {
            ((ISupportSampling)telemetry).SamplingPercentage = 100;
            exceptionTelemetry.ProactiveSamplingDecision = SamplingDecision.SampledIn;
        }       
    }
}
