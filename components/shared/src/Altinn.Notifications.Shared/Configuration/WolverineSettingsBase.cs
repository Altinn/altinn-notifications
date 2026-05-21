namespace Altinn.Notifications.Shared.Configuration;

/// <summary>
/// Base Wolverine/Azure Service Bus settings shared across all notification service components.
/// Each component extends this class to add its own queue names and retry policies.
/// </summary>
public class WolverineSettingsBase
{
    /// <summary>
    /// Determines whether Wolverine and Azure Service Bus should be configured.
    /// Defaults to true — Azure Service Bus is the active messaging path.
    /// </summary>
    public bool EnableWolverine { get; set; } = true;

    /// <summary>
    /// Connection string for Azure Service Bus.
    /// </summary>
    public string ServiceBusConnectionString { get; set; } = string.Empty;
}
