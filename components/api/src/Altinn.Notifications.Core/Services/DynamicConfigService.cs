using System.Globalization;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <inheritdoc />
public sealed class DynamicConfigService(
    IDynamicConfigRepository repository,
    ILogger<DynamicConfigService> logger) : IDynamicConfigService
{
    private static readonly TimeSpan HeartbeatMargin = TimeSpan.FromSeconds(30);
    private readonly Guid _podId = Guid.CreateVersion7();
    private int _podCount = 2;

    /// <inheritdoc />
    public async Task<(NotificationConfig Config, int PodCount)> GetDynamicConfig(
        NotificationConfig staticConfig,
        TimeSpan refreshInterval,
        CancellationToken cancellationToken)
    {
        NotificationConfig config = staticConfig.Copy();
        IReadOnlyDictionary<string, string> overrides;
        try
        {
            _podCount = await repository.GetPodCount(_podId, refreshInterval + HeartbeatMargin, cancellationToken);
            overrides = await repository.GetConfig(cancellationToken);

            config.EmailDaytimeNotificationsProcessLoopConfig = ApplyOverride(staticConfig.EmailDaytimeNotificationsProcessLoopConfig, nameof(NotificationConfig.EmailDaytimeNotificationsProcessLoopConfig));
            config.EmailAnytimeNotificationsProcessLoopConfig = ApplyOverride(staticConfig.EmailAnytimeNotificationsProcessLoopConfig, nameof(NotificationConfig.EmailAnytimeNotificationsProcessLoopConfig));
            config.ComposedEmailNotificationsProcessLoopConfig = ApplyOverride(staticConfig.ComposedEmailNotificationsProcessLoopConfig, nameof(NotificationConfig.ComposedEmailNotificationsProcessLoopConfig));
            config.SmsDaytimeNotificationsProcessLoopConfig = ApplyOverride(staticConfig.SmsDaytimeNotificationsProcessLoopConfig, nameof(NotificationConfig.SmsDaytimeNotificationsProcessLoopConfig));
            config.SmsAnytimeNotificationsProcessLoopConfig = ApplyOverride(staticConfig.SmsAnytimeNotificationsProcessLoopConfig, nameof(NotificationConfig.SmsAnytimeNotificationsProcessLoopConfig));
            config.PastDueOrdersProcessLoopConfig = ApplyOverride(staticConfig.PastDueOrdersProcessLoopConfig, nameof(NotificationConfig.PastDueOrdersProcessLoopConfig));
            config.RetryOrdersProcessLoopConfig = ApplyOverride(staticConfig.RetryOrdersProcessLoopConfig, nameof(NotificationConfig.RetryOrdersProcessLoopConfig));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read dynamic configuration from the database. Using static configuration.");
        }

        return (config, _podCount);

        ProcessLoopConfig ApplyOverride(ProcessLoopConfig loopConfig, string section)
        {
            string key = $"{nameof(NotificationConfig)}:{section}:{nameof(ProcessLoopConfig.TaskCount)}";
            if (!overrides.TryGetValue(key, out string? value))
            {
                return loopConfig;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int taskCount) || taskCount < 0)
            {
                logger.LogWarning("Ignoring dynamic configuration for {ConfigKey}: expected a non-negative integer TaskCount.", key);
                return loopConfig;
            }

            return new ProcessLoopConfig
            {
                TaskCount = taskCount,
                PrimaryTaskIdleDelaySeconds = loopConfig.PrimaryTaskIdleDelaySeconds,
                AdditionalTasksIdleDelaySeconds = loopConfig.AdditionalTasksIdleDelaySeconds,
                RampUpLimit = loopConfig.RampUpLimit
            };
        }
    }
}
