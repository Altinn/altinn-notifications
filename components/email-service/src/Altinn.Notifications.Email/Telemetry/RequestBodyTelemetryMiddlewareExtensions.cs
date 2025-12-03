namespace Altinn.Notifications.Email.Telemetry;

/// <summary>
/// Extension methods for adding request body telemetry middleware to the application pipeline.
/// </summary>
public static class RequestBodyTelemetryMiddlewareExtensions
{
    /// <summary>
    /// Adds the request body telemetry middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseRequestBodyTelemetry(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestBodyTelemetryMiddleware>();
    }
}
