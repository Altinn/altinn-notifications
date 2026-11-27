namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Persists pod heartbeats and reads processing configuration overrides.
/// </summary>
public interface IDynamicConfigRepository
{
    /// <summary>
    /// Refreshes the current pod, removes expired registrations, and returns the active pod count.
    /// </summary>
    Task<int> GetPodCount(Guid podId, TimeSpan heartbeatTimeout, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the stored configuration overrides as flat configuration keys and values.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetConfig(CancellationToken cancellationToken);
}
