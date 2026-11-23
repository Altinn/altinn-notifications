namespace Altinn.Notifications.Shared.Health;

/// <summary>
/// Tags that assign health checks to the endpoints they are evaluated by.
/// A check can carry several tags.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Identifies a check of an external dependency, such as the Wolverine listeners and the Azure Service Bus queues.
    /// It is reported on <c>/health</c> but does not affect <c>/health/ready</c>.
    /// </summary>
    public const string Dependency = "dependency";

    /// <summary>
    /// Identifies a check of the application's own lifecycle state, such as startup completion or shutdown.
    /// </summary>
    public const string Lifecycle = "lifecycle";

    /// <summary>
    /// Identifies a check of the process itself. It is evaluated by <c>/health/live</c> and must not depend on external resources.
    /// </summary>
    public const string Live = "live";

    /// <summary>
    /// Identifies a check that must pass before the application can receive traffic. It is evaluated by <c>/health/ready</c>.
    /// </summary>
    public const string Ready = "ready";

    /// <summary>
    /// Identifies a check that verifies that application startup has completed. It is evaluated by <c>/health/startup</c>.
    /// </summary>
    public const string Startup = "startup";
}
