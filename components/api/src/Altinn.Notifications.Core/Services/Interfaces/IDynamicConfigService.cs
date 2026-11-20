using Altinn.Notifications.Core.Configuration;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides runtime processing configuration and the active pod count.
/// </summary>
public interface IDynamicConfigService
{
    /// <summary>
    /// Refreshes the pod heartbeat and applies stored task count overrides without modifying the static configuration.
    /// </summary>
    Task<(NotificationConfig Config, int PodCount)> GetDynamicConfig(
        NotificationConfig staticConfig,
        TimeSpan refreshInterval,
        CancellationToken cancellationToken);
}
