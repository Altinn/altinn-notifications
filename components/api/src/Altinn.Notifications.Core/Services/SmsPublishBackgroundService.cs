using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that runs a dedicated SMS publishing loop per <see cref="SendingTimePolicy"/>.
/// </summary>
public class SmsPublishBackgroundService : SendingTimePolicyPublishBackgroundService
{
    private readonly ISmsNotificationService _smsNotificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsPublishBackgroundService"/> class.
    /// </summary>
    public SmsPublishBackgroundService(
        ISmsPublishTaskQueue smsPublishTaskQueue,
        ILogger<SmsPublishBackgroundService> logger,
        ISmsNotificationService smsNotificationService)
        : base(smsPublishTaskQueue, logger)
    {
        _smsNotificationService = smsNotificationService;
    }

    /// <inheritdoc/>
    protected override string NotificationType => "SMS";

    /// <inheritdoc/>
    protected override Task PublishAsync(SendingTimePolicy sendingTimePolicy, CancellationToken cancellationToken)
    {
        return _smsNotificationService.SendNotifications(cancellationToken, sendingTimePolicy);
    }
}
