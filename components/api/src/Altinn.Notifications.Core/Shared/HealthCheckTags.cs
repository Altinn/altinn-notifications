namespace Altinn.Notifications.Core.Shared;

/// <summary>
/// Tags used to group health checks into liveness and readiness probes.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Checks that only verify the process is alive and must not depend on external resources.
    /// </summary>
    public const string Live = "live";

    /// <summary>
    /// Checks verifying that the application has finished starting up.
    /// </summary>
    public const string Startup = "startup";

    /// <summary>
    /// Checks reflecting the application lifecycle (starting up or shutting down) rather than the health of a dependency.
    /// They gate probes but are excluded from the health check metric.
    /// </summary>
    public const string Lifecycle = "lifecycle";

    /// <summary>
    /// Checks verifying that hard dependencies are available and the application can serve traffic.
    /// </summary>
    public const string Ready = "ready";

    /// <summary>
    /// Checks for dependencies the application can run degraded without. They are reported in monitoring but must not gate readiness.
    /// </summary>
    public const string Dependency = "dependency";
}
