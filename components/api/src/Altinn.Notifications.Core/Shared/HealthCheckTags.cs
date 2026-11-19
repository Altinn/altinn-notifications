namespace Altinn.Notifications.Core.Shared;

/// <summary>
/// Tags that assign health checks to the endpoints they are evaluated by and to the health check metric.
/// A check can carry several tags.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Identifies a check of an external dependency that the application can operate without.
    /// The result is reported in the health check metric and on <c>/health</c>, but does not affect <c>/health/ready</c>.
    /// </summary>
    public const string Dependency = "dependency";

    /// <summary>
    /// Identifies a check of the application's own lifecycle state, such as startup completion or shutdown.
    /// The result is evaluated by the probe endpoints but is excluded from the health check metric.
    /// </summary>
    public const string Lifecycle = "lifecycle";

    /// <summary>
    /// Identifies a check of the process itself. It is evaluated by <c>/health/live</c> and must not depend on external resources.
    /// </summary>
    public const string Live = "live";

    /// <summary>
    /// Identifies a check that must pass before the application can receive traffic.
    /// It is evaluated by <c>/health/ready</c> and covers required dependencies as well as lifecycle state.
    /// </summary>
    public const string Ready = "ready";

    /// <summary>
    /// Identifies a check that verifies that application startup has completed. It is evaluated by <c>/health/startup</c>.
    /// </summary>
    public const string Startup = "startup";
}
