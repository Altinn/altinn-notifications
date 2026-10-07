using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that runs a dedicated email publishing loop per <see cref="SendingTimePolicy"/>.
/// </summary>
public class EmailPublishBackgroundService : SendingTimePolicyPublishBackgroundService
{
    private readonly IEmailNotificationService _emailNotificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailPublishBackgroundService"/> class.
    /// </summary>
    public EmailPublishBackgroundService(
        IEmailPublishTaskQueue emailPublishTaskQueue,
        ILogger<EmailPublishBackgroundService> logger,
        IEmailNotificationService emailNotificationService)
        : base(emailPublishTaskQueue, logger)
    {
        _emailNotificationService = emailNotificationService;
    }

    /// <inheritdoc/>
    protected override string NotificationType => "email";

    /// <inheritdoc/>
    protected override Task PublishAsync(SendingTimePolicy sendingTimePolicy, CancellationToken cancellationToken)
    {
        return _emailNotificationService.SendNotifications(cancellationToken, sendingTimePolicy);
    }
}
