namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Service that coordinates trigger operations for notifications.
/// Groups related operations to reduce constructor parameter count.
/// </summary>
public interface ITerminateExpiredNotificationsService
{
    /// <summary>
    /// Terminates expired notifications for both email and SMS.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task TerminateExpiredNotifications();
}
