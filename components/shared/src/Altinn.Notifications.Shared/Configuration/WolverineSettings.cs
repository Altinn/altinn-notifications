namespace Altinn.Notifications.Shared.Configuration;

/// <summary>
/// Base Wolverine/Azure Service Bus settings shared across all notification service components.
/// Each component extends this class to add its own queue names and retry policies.
/// </summary>
public class WolverineSettings
{
    /// <summary>
    /// Indicates whether Azure Service Bus should be configured.
    /// Defaults to false — Kafka remains active when this is false.
    /// </summary>
    public bool EnableServiceBus { get; set; } = false;

    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Number of listeners per queue per pod.
    /// </summary>
    public int ListenerCount { get; set; } = 10;
}
