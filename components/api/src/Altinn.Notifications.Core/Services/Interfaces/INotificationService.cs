namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Defines a set of service features shared between email and SMS notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Processes hanging notifications that have been set to accepted or succeeded, but never reached a final stage.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task TerminateExpiredNotifications();
}
