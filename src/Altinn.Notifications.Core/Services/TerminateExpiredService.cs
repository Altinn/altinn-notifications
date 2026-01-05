using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Service that coordinates trigger operations for notifications.
/// </summary>
public class TerminateExpiredService : ITerminateExpiredNotificationsService
{
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly ISmsNotificationService _smsNotificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminateExpiredService"/> class.
    /// </summary>
    public TerminateExpiredService(
        IEmailNotificationService emailNotificationService,
        ISmsNotificationService smsNotificationService)
    {
        _emailNotificationService = emailNotificationService;
        _smsNotificationService = smsNotificationService;
    }

    /// <inheritdoc/>
    public async Task TerminateExpiredNotifications()
    {
        await _emailNotificationService.TerminateExpiredNotifications();
        await _smsNotificationService.TerminateExpiredNotifications();
    }
}
